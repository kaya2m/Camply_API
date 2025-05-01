namespace Camply.Application.Common.Models
{
    /// <summary>
    /// Sayfalanmış yanıt
    /// </summary>
    /// <typeparam name="T">Yanıt ögesi türü</typeparam>
    public class PagedResponse<T>
    {
        /// <summary>
        /// Öğe listesi
        /// </summary>
        public List<T> Items { get; set; } = new List<T>();

        /// <summary>
        /// Mevcut sayfa numarası
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// Sayfa başına öğe sayısı
        /// </summary>
        public int PageSize { get; set; }

        /// <summary>
        /// Toplam sayfa sayısı
        /// </summary>
        public int TotalPages { get; set; }

        /// <summary>
        /// Toplam öğe sayısı
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// Önceki sayfa var mı?
        /// </summary>
        public bool HasPreviousPage => PageNumber > 1;

        /// <summary>
        /// Sonraki sayfa var mı?
        /// </summary>
        public bool HasNextPage => PageNumber < TotalPages;
    }
}
