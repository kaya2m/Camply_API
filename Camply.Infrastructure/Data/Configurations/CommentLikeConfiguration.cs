using Camply.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Camply.Infrastructure.Data.Configurations
{
    public class CommentLikeConfiguration : IEntityTypeConfiguration<CommentLike>
    {
        public void Configure(EntityTypeBuilder<CommentLike> builder)
        {
            builder.ToTable("CommentLikes");

            builder.HasKey(cl => cl.Id);

            // Unique constraint: One user can only like a comment once
            builder.HasIndex(cl => new { cl.UserId, cl.CommentId })
                .IsUnique()
                .HasDatabaseName("IX_CommentLikes_UserId_CommentId");

            // Foreign key relationships
            builder.HasOne(cl => cl.User)
                .WithMany()
                .HasForeignKey(cl => cl.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(cl => cl.Comment)
                .WithMany()
                .HasForeignKey(cl => cl.CommentId)
                .OnDelete(DeleteBehavior.Cascade);

            // Properties
            builder.Property(cl => cl.UserId)
                .IsRequired();

            builder.Property(cl => cl.CommentId)
                .IsRequired();
        }
    }
}