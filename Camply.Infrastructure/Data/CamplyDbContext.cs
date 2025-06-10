using Camply.Domain;
using Camply.Domain.Auth;
using Camply.Domain.Common;
using Camply.Domain.MachineLearning;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Camply.Infrastructure.Data
{
    public class CamplyDbContext : DbContext
    {
        private readonly ICurrentUserService _currentUserService;
        private readonly IDateTime _dateTime;

        public CamplyDbContext(
            DbContextOptions<CamplyDbContext> options,
          ICurrentUserService currentUserService = null,
            IDateTime dateTime = null) : base(options)
        {
            _currentUserService = currentUserService;
            _dateTime = dateTime;
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<Follow> Follows { get; set; }
        public DbSet<Media> Media { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<PostTag> PostTags { get; set; }
        public DbSet<BlogTag> BlogTags { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<BlogCategory> BlogCategories { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<UserRole> UserRoles { get; set; }
        public DbSet<SocialLogin> SocialLogins { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Location> Locations { get; set; }
        public DbSet<LocationReview> LocationReviews { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        // ML DbSets
        public DbSet<MLUserFeature> MLUserFeatures { get; set; }
        public DbSet<MLContentFeature> MLContentFeatures { get; set; }
        public DbSet<MLModel> MLModels { get; set; }
        public DbSet<MLTrainingJob> MLTrainingJobs { get; set; }
        public DbSet<MLExperiment> MLExperiments { get; set; }
        public DbSet<MLUserExperiment> MLUserExperiments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            var assembly = typeof(CamplyDbContext).Assembly; 

            var configTypes = assembly
                .GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface
                    && t.GetInterfaces().Any(i => i.IsGenericType
                        && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)));

       
            // Her bir konfigürasyonu tek tek uygula
            foreach (var configType in configTypes)
            {
                
                    var config = Activator.CreateInstance(configType);
                    var entityType = configType.GetInterfaces()
                        .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>))
                        .GetGenericArguments()[0];

                    System.Diagnostics.Debug.WriteLine($"Applying configuration for entity type: {entityType.Name}");

                    var applyConfigMethod = typeof(ModelBuilder)
                        .GetMethod("ApplyConfiguration")
                        .MakeGenericMethod(entityType);

                    applyConfigMethod.Invoke(modelBuilder, new[] { config });
            }

            // Soft delete filtreleri için mevcut kodunuz
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ISoftDelete).IsAssignableFrom(entityType.ClrType))
                {
                    var parameter = Expression.Parameter(entityType.ClrType, "e");
                    var propertyMethodInfo = typeof(EF).GetMethod(nameof(EF.Property)).MakeGenericMethod(typeof(bool));
                    var isDeletedProperty = Expression.Call(propertyMethodInfo, parameter, Expression.Constant("IsDeleted"));
                    var notIsDeletedValue = Expression.Not(isDeletedProperty);
                    var lambda = Expression.Lambda(notIsDeletedValue, parameter);
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
                }
            }
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedBy = _currentUserService.UserId;
                        entry.Entity.CreatedAt = _dateTime.UtcNow;
                        break;
                    case EntityState.Modified:
                        entry.Entity.LastModifiedBy = _currentUserService.UserId;
                        entry.Entity.LastModifiedAt = _dateTime.UtcNow;
                        break;
                    case EntityState.Deleted:
                        entry.State = EntityState.Modified;
                        entry.Entity.IsDeleted = true;
                        entry.Entity.DeletedBy = _currentUserService.UserId;
                        entry.Entity.DeletedAt = _dateTime.UtcNow;
                        break;
                }
            }

            foreach (var entry in ChangeTracker.Entries<BaseTrackingEntity>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = _dateTime.UtcNow;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
