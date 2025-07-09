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
    public class PostConfiguration : IEntityTypeConfiguration<Post>
    {
        public void Configure(EntityTypeBuilder<Post> builder)
        {
            builder.ToTable("Posts");

            builder.HasKey(p => p.Id);

            builder.Property(p => p.Content)
                .IsRequired();

            // User-Post ilişkisi
            builder.HasOne(p => p.User)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Location ilişkisi
            builder.HasOne(p => p.Location)
                .WithMany(l => l.Posts)
                .HasForeignKey(p => p.LocationId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // Optimized indexes for feed queries
            builder.HasIndex(p => new { p.UserId, p.CreatedAt });
            builder.HasIndex(p => new { p.Status, p.CreatedAt }); // For general posts ordering
            builder.HasIndex(p => new { p.Status, p.ViewCount }); // For popular posts
            builder.HasIndex(p => p.LocationId);
            builder.HasIndex(p => new { p.LocationId, p.Status, p.CreatedAt }); // For location-based feeds
            builder.HasIndex(p => p.CreatedAt); // For quick date sorting
        }
    }
}
