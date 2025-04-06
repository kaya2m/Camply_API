using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Camply.Infrastructure.Data.Configurations
{
    public class MediaConfiguration : IEntityTypeConfiguration<Media>
    {
        public void Configure(EntityTypeBuilder<Media> builder)
        {
            builder.ToTable("Media");

            builder.HasKey(m => m.Id);

            builder.Property(m => m.FileName)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(m => m.FileType)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(m => m.MimeType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(m => m.Url)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(m => m.ThumbnailUrl)
                .HasMaxLength(500);

            builder.Property(m => m.Description)
                .HasMaxLength(500);

            builder.Property(m => m.AltTag)
                .HasMaxLength(255);

            builder.Property(m => m.EntityType)
                .HasMaxLength(50);

            builder.Property(m => m.Metadata)
                .HasColumnType("jsonb");

            // Indexes
            builder.HasIndex(m => m.EntityId);
            builder.HasIndex(m => m.Type);
        }
    }
}
