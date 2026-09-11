using Homassy.API.Entities.Activity;
using Homassy.API.Entities.Common;
using Homassy.API.Entities.Family;
using Homassy.API.Entities.Location;
using Homassy.API.Entities.Product;
using Homassy.API.Entities.ShoppingList;
using Homassy.API.Entities.User;
using Homassy.API.Models.Common;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Text.Json;

namespace Homassy.API.Context
{
    public class HomassyDbContext : DbContext
    {
        private bool _readOnly;

        /// <summary>
        /// The only constructor: a context is always configured by whoever creates it, which in
        /// this application is the DI registration in <c>Program.cs</c> (through
        /// <see cref="IDbContextFactory{TContext}"/> or the scoped registration) and the
        /// design-time <see cref="HomassyDbContextFactory"/> for EF tooling.
        /// </summary>
        /// <remarks>
        /// There is deliberately no parameterless constructor. One used to exist, backed by a
        /// static configuration hook and an <c>OnConfiguring</c> fallback, so that the Functions
        /// layer could write <c>new HomassyDbContext()</c>. That made the connection string
        /// process-wide mutable state, and it is what let a context be created from anywhere —
        /// including places with no scope to dispose it.
        /// </remarks>
        public HomassyDbContext(DbContextOptions<HomassyDbContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Turns this context into the read-only context described on
        /// <see cref="HomassyDbContextFactoryExtensions.CreateForReading"/>.
        /// </summary>
        /// <remarks>
        /// Separate from the constructor because a context handed out by
        /// <see cref="IDbContextFactory{TContext}"/> is already built by the time the caller can
        /// say which mode it wants.
        /// </remarks>
        internal void MarkReadOnly()
        {
            _readOnly = true;
            ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTrackingWithIdentityResolution;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure PublicId for all BaseEntity types
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property("PublicId")
                        .HasDefaultValueSql("gen_random_uuid()")
                        .ValueGeneratedOnAdd();
                }
            }

            // Global query filter for soft delete - automatically applied to all SoftDeleteEntity types
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(SoftDeleteEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var property = Expression.Property(parameter, nameof(SoftDeleteEntity.IsDeleted));
                    var filter = Expression.Lambda(Expression.Not(property), parameter);

                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
                }
            }

            #region User Relationships
            modelBuilder.Entity<User>()
                .HasOne(u => u.Profile)
                .WithOne(p => p.User)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasOne(u => u.NotificationPreferences)
                .WithOne(n => n.User)
                .HasForeignKey<UserNotificationPreferences>(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.PushSubscriptions)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<UserPushSubscription>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Endpoint).IsUnique();
            });

            // No navigation from User to its picture on purpose: the whole point of the separate
            // table is that loading a user never loads the bytes, and a navigation is an
            // invitation to Include() them.
            modelBuilder.Entity<UserProfilePicture>(entity =>
            {
                entity.HasOne(p => p.User)
                    .WithMany()
                    .HasForeignKey(p => p.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.UserId).IsUnique();
            });

            // Badge earned state (#109). The unique index on (UserId, BadgeId) is the point of
            // this configuration, not a nicety: two concurrent requests can both cross the same
            // threshold, and a database that refuses the second insert is what makes
            // double-granting - and therefore a second unlock celebration - impossible rather
            // than merely unlikely. No navigation from User: badges are read by user id, and a
            // collection on User would invite Include()ing them into every profile load.
            modelBuilder.Entity<UserBadge>(entity =>
            {
                entity.HasOne(b => b.User)
                    .WithMany()
                    .HasForeignKey(b => b.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => new { e.UserId, e.BadgeId }).IsUnique();
            });

            // The notification centre's rows (#116). No navigation from User: notifications are
            // read by user id and paged, and a collection on User would invite Include()ing a
            // whole inbox into every profile load.
            modelBuilder.Entity<UserNotification>(entity =>
            {
                entity.HasOne(n => n.User)
                    .WithMany()
                    .HasForeignKey(n => n.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // Serves the one query that matters: this user's inbox, newest first. The
                // descending timestamp is part of the index rather than a sort afterwards
                // because every page of every read is ordered this way.
                entity.HasIndex(e => new { e.UserId, e.CreatedAt })
                    .IsDescending(false, true);

                // The unread count, which is fetched far more often than a page of the list is
                // (at boot, on every push arrival, after every read). Filtered so the index only
                // holds the rows it can answer from - an inbox is mostly read rows.
                entity.HasIndex(e => e.UserId)
                    .HasDatabaseName("IX_UserNotifications_Unread")
                    .HasFilter("\"ReadAt\" IS NULL AND \"IsDeleted\" = false");

                // Supports the retention sweep, which scans by age across every user.
                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.ParametersJson).HasColumnType("text");
            });
            #endregion

            #region FamilyExternalCalendar Relationships
            modelBuilder.Entity<FamilyExternalCalendar>(entity =>
            {
                entity.HasOne(c => c.Family)
                    .WithMany()
                    .HasForeignKey(c => c.FamilyId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.FamilyId);
                entity.HasIndex(e => new { e.FamilyId, e.IsEnabled });
                entity.Property(e => e.CachedEventsJson).HasColumnType("text");
            });

            modelBuilder.Entity<ExternalCalendarReminderDispatch>(entity =>
            {
                entity.HasOne<FamilyExternalCalendar>()
                    .WithMany()
                    .HasForeignKey(d => d.ExternalCalendarId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<User>()
                    .WithMany()
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // The occurrence key is an identifier, not an instant, so it must not be reinterpreted
                // against a timezone the way a `timestamptz` column would be.
                entity.Property(e => e.OccurrenceKey).HasColumnType("timestamp without time zone");

                // Guarantees at-most-once delivery per (calendar, member, occurrence, lead time) even if
                // two worker instances race. The explicit name keeps it under PostgreSQL's 63-char limit.
                entity.HasIndex(e => new { e.ExternalCalendarId, e.UserId, e.EventUid, e.OccurrenceKey, e.LeadTimeMinutes })
                    .HasDatabaseName("IX_ExtCalReminderDispatches_Occurrence")
                    .IsUnique();

                // Supports the worker's retention sweep.
                entity.HasIndex(e => e.SentAt)
                    .HasDatabaseName("IX_ExtCalReminderDispatches_SentAt");
            });
            #endregion

            #region FamilyChat Relationships
            // The family conversation (#144). No navigation from Family to its messages: a chat is
            // read one page at a time by its own query, and a collection on Family would invite
            // Include()ing an entire conversation into a family lookup the caches already serve.
            modelBuilder.Entity<FamilyChatMessage>(entity =>
            {
                entity.HasOne(m => m.Family)
                    .WithMany()
                    .HasForeignKey(m => m.FamilyId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(m => m.Sender)
                    .WithMany()
                    .HasForeignKey(m => m.SenderUserId)
                    .OnDelete(DeleteBehavior.Cascade);

                // The one query that matters: this family's conversation, newest first. The
                // descending timestamp is part of the index rather than a sort afterwards,
                // because every page of every read is ordered this way.
                entity.HasIndex(e => new { e.FamilyId, e.SentAt })
                    .IsDescending(false, true);
            });
            #endregion

            #region FamilyJoinRequest Relationships
            modelBuilder.Entity<FamilyJoinRequest>(entity =>
            {
                entity.HasOne(r => r.User)
                    .WithMany()
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(r => r.Family)
                    .WithMany()
                    .HasForeignKey(r => r.FamilyId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.FamilyId);
                entity.HasIndex(e => e.Status);

                // At most one active pending request per user.
                entity.HasIndex(e => e.UserId)
                    .IsUnique()
                    .HasFilter("\"Status\" = 0 AND \"IsDeleted\" = false");
            });
            #endregion

            #region Product Relationships
            // Same shape as UserProfilePicture, and for the same reason: no navigation from
            // Product, so a product query cannot pull the bytes back in.
            modelBuilder.Entity<ProductImage>(entity =>
            {
                entity.HasOne(p => p.Product)
                    .WithMany()
                    .HasForeignKey(p => p.ProductId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.ProductId).IsUnique();
            });

            modelBuilder.Entity<Product>()
                .HasMany(p => p.Customizations)
                .WithOne(c => c.Product)
                .HasForeignKey(c => c.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Product>()
                .HasMany(p => p.InventoryItems)
                .WithOne(i => i.Product)
                .HasForeignKey(i => i.ProductId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductInventoryItem>()
                .HasOne(i => i.PurchaseInfo)
                .WithOne(p => p.ProductInventoryItem)
                .HasForeignKey<ProductPurchaseInfo>(p => p.ProductInventoryItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductInventoryItem>()
                .HasMany(i => i.ConsumptionLogs)
                .WithOne(l => l.ProductInventoryItem)
                .HasForeignKey(l => l.ProductInventoryItemId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductInventoryItem>()
                .HasOne(i => i.StorageLocation)
                .WithMany(s => s.InventoryItems)
                .HasForeignKey(i => i.StorageLocationId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ProductPurchaseInfo>()
                .HasOne(p => p.ShoppingLocation)
                .WithMany(s => s.Purchases)
                .HasForeignKey(p => p.ShoppingLocationId)
                .OnDelete(DeleteBehavior.SetNull);

            // Task 11 (#128): explicit precision, or EF picks its own default and the generated
            // migration will not match what Step 5 of the task brief expects (numeric(18,4)).
            modelBuilder.Entity<ProductPurchaseInfo>()
                .Property(p => p.Price)
                .HasPrecision(18, 4);

            modelBuilder.Entity<ProductInventoryItem>()
                .HasMany(i => i.Automations)
                .WithOne(a => a.ProductInventoryItem)
                .HasForeignKey(a => a.ProductInventoryItemId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ItemAutomation>()
                .HasOne(a => a.Product)
                .WithMany()
                .HasForeignKey(a => a.ProductId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ItemAutomation>()
                .HasOne(a => a.ShoppingList)
                .WithMany()
                .HasForeignKey(a => a.ShoppingListId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<ItemAutomation>()
                .HasMany(a => a.Executions)
                .WithOne(e => e.ItemAutomation)
                .HasForeignKey(e => e.ItemAutomationId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ItemAutomation>(entity =>
            {
                entity.HasIndex(e => e.ProductInventoryItemId);
                entity.HasIndex(e => e.ProductId);
                entity.HasIndex(e => e.ShoppingListId);
                entity.HasIndex(e => new { e.IsEnabled, e.NextExecutionAt });
            });

            modelBuilder.Entity<ItemAutomationExecution>(entity =>
            {
                entity.HasIndex(e => e.ItemAutomationId);
                entity.HasIndex(e => e.ExecutedAt);
            });
            #endregion

            #region ShoppingList Relationships
            modelBuilder.Entity<ShoppingList>()
                .HasMany(sl => sl.Items)
                .WithOne(sli => sli.ShoppingList)
                .HasForeignKey(sli => sli.ShoppingListId)
                .OnDelete(DeleteBehavior.Cascade);
            #endregion

            #region Activity Indexes
            modelBuilder.Entity<Activity>(entity =>
            {
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.FamilyId);
                entity.HasIndex(e => e.Timestamp);
                entity.HasIndex(e => e.ActivityType);
                entity.HasIndex(e => new { e.UserId, e.Timestamp })
                    .IsDescending(false, true);  // UserId ASC, Timestamp DESC
                entity.HasIndex(e => new { e.FamilyId, e.Timestamp })
                    .IsDescending(false, true);  // FamilyId ASC, Timestamp DESC
            });
            #endregion
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfReadOnly();
            UpdateRecordChanges();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            ThrowIfReadOnly();
            UpdateRecordChanges();
            return base.SaveChanges();
        }

        private void ThrowIfReadOnly()
        {
            if (_readOnly)
            {
                // Without this the call would be a silent no-op: nothing is tracked, so nothing
                // is written, and the caller sees a successful save that changed nothing.
                throw new InvalidOperationException(
                    "This context came from CreateForReading() and does not track entities. " +
                    "Use CreateDbContext() for an operation that writes.");
            }
        }

        private void UpdateRecordChanges()
        {
            var userId = SessionInfo.GetUserId();
            var entities = ChangeTracker.Entries<RecordChangeEntity>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entity in entities)
            {
                entity.Entity.UpdateRecordChange(userId);
            }
        }

        // DbSets
        public DbSet<TableRecordChange> TableRecordChanges { get; set; }

        #region Activity Related DbSets
        public DbSet<Activity> Activities { get; set; }
        #endregion

        #region User Related DbSets
        public DbSet<User> Users { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<UserProfilePicture> UserProfilePictures { get; set; }
        public DbSet<UserNotificationPreferences> UserNotificationPreferences { get; set; }
        public DbSet<UserPushSubscription> UserPushSubscriptions { get; set; }
        public DbSet<UserBadge> UserBadges { get; set; }
        public DbSet<UserNotification> UserNotifications { get; set; }
        public DbSet<Family> Families { get; set; }
        public DbSet<FamilyJoinRequest> FamilyJoinRequests { get; set; }
        public DbSet<FamilyExternalCalendar> FamilyExternalCalendars { get; set; }
        public DbSet<FamilyChatMessage> FamilyChatMessages { get; set; }
        public DbSet<ExternalCalendarReminderDispatch> ExternalCalendarReminderDispatches { get; set; }
        #endregion

        #region Product Related DbSets
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<ProductInventoryItem> ProductInventoryItems { get; set; }
        public DbSet<ProductPurchaseInfo> ProductPurchaseInfos { get; set; }
        public DbSet<ProductConsumptionLog> ProductConsumptionLogs { get; set; }
        public DbSet<ProductCustomization> ProductCustomizations { get; set; }
        public DbSet<ItemAutomation> ItemAutomations { get; set; }
        public DbSet<ItemAutomationExecution> ItemAutomationExecutions { get; set; }
        #endregion

        #region Location Related DbSets
        public DbSet<ShoppingLocation> ShoppingLocations { get; set; }
        public DbSet<StorageLocation> StorageLocations { get; set; }
        #endregion

        #region Shopping list Related DbSets
        public DbSet<ShoppingList> ShoppingLists { get; set; }
        public DbSet<ShoppingListItem> ShoppingListItems { get; set; }
        #endregion
    }
}
