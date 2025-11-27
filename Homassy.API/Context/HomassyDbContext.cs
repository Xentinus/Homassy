using Homassy.API.Entities;
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

            // Faster lookups
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // Global query filter for soft delete - automatically applied to all BaseEntity types
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                    var filter = Expression.Lambda(Expression.Not(property), parameter);

                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
                }
            }
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
            var entities = ChangeTracker.Entries<BaseEntity>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

            foreach (var entity in entities)
            {
                entity.Entity.UpdateRecordChange(userId);
            }
        }

        // DbSets
        public DbSet<Family> Families { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductItem> ProductItems { get; set; }
        public DbSet<ShoppingListItem> ShoppingListItems { get; set; }
        public DbSet<ShoppingLocation> ShoppingLocations { get; set; }
        public DbSet<StorageLocation> StorageLocations { get; set; }
    }
}
