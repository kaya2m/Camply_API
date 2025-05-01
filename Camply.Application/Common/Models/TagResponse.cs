namespace Camply.Application.Common.Models
{
    /// <summary>
    /// Etiket yanıtı
    /// </summary>
    public class TagResponse
    {
        /// <summary>
        /// Etiket ID'si
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Etiket adı
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// SEO-friendly URL parçası
        /// </summary>
        public string Slug { get; set; }

        /// <summary>
        /// Kullanım sayısı
        /// </summary>
        public int UsageCount { get; set; }
    }
}
