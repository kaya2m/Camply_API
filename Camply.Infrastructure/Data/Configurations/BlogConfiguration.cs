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
    public class BlogConfiguration : IEntityTypeConfiguration<Blog>
    {
        public void Configure(EntityTypeBuilder<Blog> builder)
        {
            builder.ToTable("Blogs");

            builder.HasKey(b => b.Id);

            builder.Property(b => b.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(b => b.Slug)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(b => b.Content)
                .IsRequired();

            builder.Property(b => b.Summary)
                .HasMaxLength(500);

            builder.Property(b => b.MetaDescription)
                .HasMaxLength(160);

            builder.Property(b => b.MetaKeywords)
                .HasMaxLength(255);

            // User-Blog ilişkisi
            builder.HasOne(b => b.User)
                .WithMany(u => u.Blogs)
                .HasForeignKey(b => b.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // FeaturedImage ilişkisi
            builder.HasOne(b => b.FeaturedImage)
                .WithMany()
                .HasForeignKey(b => b.FeaturedImageId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // Location ilişkisi
            builder.HasOne(b => b.Location)
                .WithMany(l => l.Blogs)
                .HasForeignKey(b => b.LocationId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            // Indexes
            builder.HasIndex(b => b.Slug).IsUnique();
            builder.HasIndex(b => new { b.UserId, b.CreatedAt });
            builder.HasIndex(b => b.Status);
            builder.HasIndex(b => b.LocationId);
        }
    }
}
