using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Camply.Domain.Common;
using System.Xml.Linq;
using Camply.Domain.Auth;
using Camply.Domain.Enums;

namespace Camply.Domain
{
    /// <summary>
    /// Sosyal medya gönderisi varlığı
    /// </summary>
    public class Post : BaseEntity
    {
        /// <summary>
        /// Gönderiyi oluşturan kullanıcı ID'si
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Gönderi içeriği
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Gönderi türü (Normal, Anket, vs.)
        /// </summary>
        public PostType Type { get; set; }

        /// <summary>
        /// Gönderi durumu
        /// </summary>
        public PostStatus Status { get; set; }

        /// <summary>
        /// Lokasyon bilgisi (opsiyonel)
        /// </summary>
        public Guid? LocationId { get; set; }

        /// <summary>
        /// Konum adı
        /// </summary>
        public string LocationName { get; set; }

        /// <summary>
        /// Enlem (opsiyonel)
        /// </summary>
        public double? Latitude { get; set; }

        /// <summary>
        /// Boylam (opsiyonel)
        /// </summary>
        public double? Longitude { get; set; }

        /// <summary>
        /// Görüntülenme sayısı
        /// </summary>
        public int ViewCount { get; set; }

        // Navigation properties

        /// <summary>
        /// Gönderiyi oluşturan kullanıcı
        /// </summary>
        public virtual User User { get; set; }

        /// <summary>
        /// Gönderiye ait medya dosyaları
        /// </summary>
        public virtual ICollection<Media> Media { get; set; }

        /// <summary>
        /// Gönderiye yapılan yorumlar
        /// </summary>
        public virtual ICollection<Comment> Comments { get; set; }

        /// <summary>
        /// Gönderiye yapılan beğeniler
        /// </summary>
        public virtual ICollection<Like> Likes { get; set; }

        /// <summary>
        /// Gönderiye ait etiketler
        /// </summary>
        public virtual ICollection<PostTag> Tags { get; set; }

        /// <summary>
        /// Paylaşıldığı kamp lokasyonu (opsiyonel)
        /// </summary>
        public virtual Location Location { get; set; }

        public Post()
        {
            Media = new HashSet<Media>();
            Comments = new HashSet<Comment>();
            Likes = new HashSet<Like>();
            Tags = new HashSet<PostTag>();
            Type = PostType.Standard;
            Status = PostStatus.Active;
        }
    }
}
