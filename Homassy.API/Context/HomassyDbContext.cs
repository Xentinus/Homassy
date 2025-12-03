using Homassy.API.Entities.Common;
using Homassy.API.Entities.Family;
using Homassy.API.Entities.Location;
using Homassy.API.Entities.Product;
using Homassy.API.Entities.ShoppingList;
using Homassy.API.Entities.User;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Homassy.API.Context
{
    public class HomassyDbContext : DbContext
    {
        private static IConfiguration? _configuration;

        public HomassyDbContext()
        {
        }

        public HomassyDbContext(DbContextOptions<HomassyDbContext> options)
            : base(options)
        {
        }

        public static void SetConfiguration(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                if (_configuration == null)
                {
                    throw new InvalidOperationException(
                        "Configuration not set. Call HomassyDbContext.SetConfiguration() during application startup.");
                }

                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                if (string.IsNullOrEmpty(connectionString))
                {
                    throw new InvalidOperationException("DefaultConnection string not found in configuration.");
                }

                optionsBuilder.UseNpgsql(connectionString);
            }
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

            // Configure User relationships
            modelBuilder.Entity<User>()
                .HasOne(u => u.Profile)
                .WithOne(p => p.User)
                .HasForeignKey<UserProfile>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasOne(u => u.Authentication)
                .WithOne(a => a.User)
                .HasForeignKey<UserAuthentication>(a => a.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasOne(u => u.NotificationPreferences)
                .WithOne(n => n.User)
                .HasForeignKey<UserNotificationPreferences>(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configure Product relationships
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
        }

        // Update record change tracking before saving
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            UpdateRecordChanges();
            return base.SaveChangesAsync(cancellationToken);
        }

        public override int SaveChanges()
        {
            UpdateRecordChanges();
            return base.SaveChanges();
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
        public DbSet<Family> Families { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<UserAuthentication> UserAuthentications { get; set; }
        public DbSet<UserNotificationPreferences> UserNotificationPreferences { get; set; }

        public DbSet<Product> Products { get; set; }
        public DbSet<ProductInventoryItem> ProductInventoryItems { get; set; }
        public DbSet<ProductPurchaseInfo> ProductPurchaseInfos { get; set; }
        public DbSet<ProductConsumptionLog> ProductConsumptionLogs { get; set; }
        public DbSet<ProductCustomization> ProductCustomizations { get; set; }

        public DbSet<ShoppingListItem> ShoppingListItems { get; set; }
        public DbSet<ShoppingLocation> ShoppingLocations { get; set; }
        public DbSet<StorageLocation> StorageLocations { get; set; }
        public DbSet<TableRecordChange> TableRecordChanges { get; set; }
    }
}
