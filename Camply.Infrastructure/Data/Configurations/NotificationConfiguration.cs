using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Camply.Infrastructure.Data.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");

            builder.HasKey(n => n.Id);

            builder.Property(n => n.Title)
                .HasMaxLength(100);

            builder.Property(n => n.Message)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(n => n.EntityType)
                .HasMaxLength(50);

            // User-Notification ilişkisi (bildirimin hedef kullanıcısı)
            builder.HasOne(n => n.User)
                .WithMany(u => u.Notifications)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Actor-Notification ilişkisi (bildirimi tetikleyen kullanıcı)
            // Eğer Notification entity'sinde Actor özelliği varsa
            builder.HasOne(n => n.Actor)
                .WithMany() 
                .HasForeignKey(n => n.ActorId)
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired(false);  // ActorId null olabilir

            builder.HasIndex(n => n.UserId);
            builder.HasIndex(n => n.IsRead);
        }
    }
}
