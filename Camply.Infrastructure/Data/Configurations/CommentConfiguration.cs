using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Camply.Domain;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Camply.Infrastructure.Data.Configurations
{
    public class CommentConfiguration : IEntityTypeConfiguration<Comment>
    {
        public void Configure(EntityTypeBuilder<Comment> builder)
        {
            builder.ToTable("Comments");

            builder.HasKey(c => c.Id);

            builder.Property(c => c.Content)
                .IsRequired()
                .HasMaxLength(1000);

            builder.Property(c => c.EntityType)
                .IsRequired()
                .HasMaxLength(50);

            // User-Comment ilişkisi
            builder.HasOne(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Parent Comment ilişkisi (replies)
            builder.HasOne(c => c.Parent)
                .WithMany()  // Eğer Parent comment'in bir Replies koleksiyonu varsa, burayı "WithMany(c => c.Replies)" olarak değiştirin
                .HasForeignKey(c => c.ParentId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);

            // Indexes
            builder.HasIndex(c => new { c.EntityId, c.EntityType });
            builder.HasIndex(c => c.UserId);
            builder.HasIndex(c => c.ParentId);
        }
    }
}
