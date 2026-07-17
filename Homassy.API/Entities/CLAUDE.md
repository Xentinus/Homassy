# Entities & Database — Homassy.API

> Split out of [../CLAUDE.md](../CLAUDE.md). Entity inheritance hierarchy, the PostgreSQL trigger system, and per-request session context.

### Entity Inheritance Hierarchy

All entities follow a clear inheritance hierarchy that provides built-in functionality:

```
BaseEntity (abstract)
  ├── Id: int (primary key, auto-generated)
  └── PublicId: Guid (auto-generated via gen_random_uuid())
      │
      └── SoftDeleteEntity
          └── IsDeleted: bool
              │
              └── RecordChangeEntity
                  └── RecordChange: JSON string (LastModifiedDate, LastModifiedBy)
                      │
                      └── User, Family, Product, ShoppingList, Location,
                          ItemAutomation, ItemAutomationExecution,
                          FamilyJoinRequest, etc.
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

