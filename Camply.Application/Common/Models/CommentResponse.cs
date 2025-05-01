namespace Camply.Application.Common.Models
{
    /// <summary>
    /// Yorum yanıtı
    /// </summary>
    public class CommentResponse
    {
        /// <summary>
        /// Yorum ID'si
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Yorum yapan kullanıcı ID'si
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Yorum yapan kullanıcı adı
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// Yorum yapan kullanıcı profil resmi
        /// </summary>
        public string UserProfileImage { get; set; }

        /// <summary>
        /// Yorum içeriği
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Yorum tarihi
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Üst yorumun ID'si (yanıt ise)
        /// </summary>
        public Guid? ParentId { get; set; }

        /// <summary>
        /// Yanıtlar listesi
        /// </summary>
        public List<CommentResponse> Replies { get; set; } = new List<CommentResponse>();
    }
}
