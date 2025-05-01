using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Common.Models
{
    /// <summary>
    /// Medya özet yanıtı
    /// </summary>
    public class MediaSummaryResponse
    {
        /// <summary>
        /// Medya ID'si
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Medya dosya URL'i
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Küçük resim URL'i
        /// </summary>
        public string ThumbnailUrl { get; set; }

        /// <summary>
        /// Dosya türü (.jpg, .png vb.)
        /// </summary>
        public string FileType { get; set; }

        /// <summary>
        /// Medya açıklaması
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Alternatif metin (SEO için)
        /// </summary>
        public string AltTag { get; set; }

        /// <summary>
        /// Medya genişliği (piksel)
        /// </summary>
        public int? Width { get; set; }

        /// <summary>
        /// Medya yüksekliği (piksel)
        /// </summary>
        public int? Height { get; set; }
    }
}
