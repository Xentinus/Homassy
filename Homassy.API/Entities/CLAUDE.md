# Entities & Database — Homassy.API

> Split out of [../CLAUDE.md](../CLAUDE.md). Entity inheritance hierarchy, the PostgreSQL trigger system, and per-request session context.

### Entity Inheritance Hierarchy

All entities follow a clear inheritance hierarchy that provides built-in functionality:

```
BaseEntity (abstract)
  ├── Id: int (primary key, auto-generated)
  └── PublicId: Guid (auto-generated via gen_random_uuid())
      │
      ├── SoftDeleteEntity
      │   └── IsDeleted: bool
      │       │
      │       └── RecordChangeEntity
      │           └── RecordChange: JSON string (LastModifiedDate, LastModifiedBy)
      │               │
      │               └── User, Family, Product, ShoppingList, Location,
      │                   ItemAutomation, ItemAutomationExecution,
      │                   FamilyJoinRequest, etc.
      │
      └── StoredImageEntity (abstract)
          └── Data / Format / Width / Height / ThumbnailData / ThumbnailFormat
              / Version / UpdatedAt
              │
              └── UserProfilePicture, ProductImage
```

#### BaseEntity

The foundation for all entities:

```csharp
public abstract class BaseEntity
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public Guid PublicId { get; set; }
}
```

- **Id**: Internal integer primary key
- **PublicId**: Externally-facing GUID identifier (prevents ID enumeration attacks)

#### SoftDeleteEntity

Adds soft delete capability:

```csharp
public class SoftDeleteEntity : BaseEntity
{
    public bool IsDeleted { get; set; } = false;

    public void DeleteRecord()
    {
        IsDeleted = true;
    }
}
```

- Global query filter automatically excludes `IsDeleted = true` records
- Records are never physically deleted, only marked as deleted

#### RecordChangeEntity

Adds automatic change tracking:

```csharp
public class RecordChangeEntity : SoftDeleteEntity
{
    public string RecordChange { get; set; } = JsonSerializer.Serialize(new RecordChange());

    public void UpdateRecordChange(int? modifiedBy = null)
    {
        RecordChange = JsonSerializer.Serialize(new RecordChange
        {
            LastModifiedDate = DateTime.UtcNow,
            LastModifiedBy = modifiedBy ?? -1
        });
    }

    public void DeleteRecord(int? modifiedBy = null)
    {
        IsDeleted = true;
        UpdateRecordChange(modifiedBy);
    }
}
```

- Tracks `LastModifiedDate` and `LastModifiedBy` in JSON format
- Automatically updated via `DbContext.SaveChanges` override

#### StoredImageEntity

Uploaded picture bytes, in a table of their own rather than as a column on the row they belong to:

```
UserProfilePicture  →  UserProfilePictures  (unique FK to Users, cascade delete)
ProductImage        →  ProductImages        (unique FK to Products, cascade delete)
FamilyPicture       →  FamilyPictures       (unique FK to Families, cascade delete)
```

Two reasons, which are really the same reason — the bytes were part of the entity:

1. The Functions layer holds whole entities in process-wide caches, so a blob column on `UserProfiles`, `Products` or `Families` is a blob resident in memory for every row a family owns.
2. Everything that serialized the row serialized the image with it, uncacheably, in every payload.

The owning row keeps only `…PictureVersion` — a 16-hex-character content hash — which is all `Constants/MediaUrls` needs to build a URL. These tables are read by the image endpoints and by nothing else, so there is deliberately **no navigation property** from `User` or `Product` to its image: a navigation is an invitation to `Include()` the bytes back into a list query.

It descends from `BaseEntity`, not `RecordChangeEntity`: there is nothing to soft-delete (deleting the picture deletes the row) and no cache to invalidate, so it stays out of the trigger system below.

### The two tables that are deliberately outside the trigger system

`UserNotifications` (#116) descends from `RecordChangeEntity` like everything else, but
`DatabaseTriggerInitializer` skips it by name. Nothing caches it - an inbox is per-user, changes
from out-of-process workers, and is read once when the drawer opens - so a trigger would only add
a `TableRecordChanges` insert and a `pg_notify` to every write on what is now the
highest-volume table in the schema: one row per family member per worker event, all of it
invalidating nothing.

Its own configuration is worth reading in `OnModelCreating`: a descending
`(UserId, CreatedAt)` index for the one query that matters, a *filtered* index on `UserId` for the
unread count (fetched far more often than a page is, and an inbox is mostly read rows), and a
`CreatedAt` index for the retention sweep.

`FamilyChatMessages` (#144) is skipped by name for the same reason. A conversation is write-heavy,
is read as one page when the chat panel opens, and is pushed onward over SignalR rather than out of
a cache — so a trigger on it would add a `TableRecordChanges` insert and a `pg_notify` to every
message sent, invalidating nothing. It carries one index, the descending `(FamilyId, SentAt)` pair
every page of every read is ordered by, and no navigation from `Family`: a collection there would
invite `Include()`ing a whole conversation into a family lookup the caches already serve.

### Database Trigger System

PostgreSQL triggers automatically track changes for cache invalidation:

1. **Trigger Function**: `record_table_change()` PostgreSQL function
2. **Automatic Triggers**: Created for all `RecordChangeEntity` descendants
3. **Change Table**: `TableRecordChanges` table stores change notifications
4. **Initialization**: `DatabaseTriggerInitializer` sets up triggers on startup

**Flow:**
```
1. Entity updated in database
2. PostgreSQL trigger fires
3. Record inserted into TableRecordChanges
4. Cache system reads changes and invalidates affected caches
```

### Session Context Management

The `SessionInfo` static class provides user context throughout the application via `AsyncLocal`:

```csharp
public static class SessionInfo
{
    private static readonly AsyncLocal<Guid?> _publicId = new();
    private static readonly AsyncLocal<int?> _userId = new();
    private static readonly AsyncLocal<int?> _familyId = new();

    public static Guid? GetPublicId() => _publicId.Value;
    public static int? GetUserId() => _userId.Value;
    public static int? GetFamilyId() => _familyId.Value;

    public static void SetUser(Guid? publicId, int? familyId = null) { ... }
    public static void Clear() { ... }
}
```

- **AsyncLocal Storage**: Thread-safe, request-scoped storage
- **Set by Middleware**: `SessionInfoMiddleware` extracts claims from JWT
- **Cleared After Request**: Ensures no data leakage between requests
- **Global Access**: Any part of the application can access current user context

---

