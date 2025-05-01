using System.ComponentModel.DataAnnotations;

namespace Camply.Application.Common.Models
{
    /// <summary>
    /// Yorum oluşturma isteği
    /// </summary>
    public class CreateCommentRequest
    {
        /// <summary>
        /// Yorum içeriği
        /// </summary>
        [Required]
        [StringLength(1000, MinimumLength = 1)]
        public string Content { get; set; }

        /// <summary>
        /// Üst yorumun ID'si (yanıt ise)
        /// </summary>
        public Guid? ParentId { get; set; }
    }
}
