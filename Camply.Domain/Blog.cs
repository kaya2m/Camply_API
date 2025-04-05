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
    /// Blog yazısı varlığı
    /// </summary>
    public class Blog : BaseEntity
    {
        /// <summary>
        /// Blog yazısının yazarı (kullanıcı ID'si)
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Blog başlığı
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// SEO-friendly URL
        /// </summary>
        public string Slug { get; set; }

        /// <summary>
        /// Blog içeriği (HTML veya Markdown)
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Özet
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Öne çıkan resim ID'si
        /// </summary>
        public Guid? FeaturedImageId { get; set; }

        /// <summary>
        /// Blog yazısı durumu
        /// </summary>
        public BlogStatus Status { get; set; }

        /// <summary>
        /// Yayınlanma tarihi
        /// </summary>
        public DateTime? PublishedAt { get; set; }

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

        /// <summary>
        /// SEO meta açıklaması
        /// </summary>
        public string MetaDescription { get; set; }

        /// <summary>
        /// SEO anahtar kelimeleri
        /// </summary>
        public string MetaKeywords { get; set; }

        // Navigation properties

        /// <summary>
        /// Blog yazarı
        /// </summary>
        public virtual User User { get; set; }

        /// <summary>
        /// Öne çıkan resim
        /// </summary>
        public virtual Media FeaturedImage { get; set; }

        /// <summary>
        /// Blog yazısına ait medya dosyaları
        /// </summary>
        public virtual ICollection<Media> Media { get; set; }

        /// <summary>
        /// Blog yazısına yapılan yorumlar
        /// </summary>
        public virtual ICollection<Comment> Comments { get; set; }

        /// <summary>
        /// Blog yazısına yapılan beğeniler
        /// </summary>
        public virtual ICollection<Like> Likes { get; set; }

        /// <summary>
        /// Blog yazısının kategorileri
        /// </summary>
        public virtual ICollection<BlogCategory> Categories { get; set; }

        /// <summary>
        /// Blog yazısının etiketleri
        /// </summary>
        public virtual ICollection<BlogTag> Tags { get; set; }

        /// <summary>
        /// İlişkili kamp lokasyonu (opsiyonel)
        /// </summary>
        public virtual Location Location { get; set; }

        public Blog()
        {
            Media = new HashSet<Media>();
            Comments = new HashSet<Comment>();
            Likes = new HashSet<Like>();
            Categories = new HashSet<BlogCategory>();
            Tags = new HashSet<BlogTag>();
            Status = BlogStatus.Draft;
        }
    }
}
