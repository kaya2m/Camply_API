using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Camply.Domain.Auth;
using Camply.Domain.Common;
using Camply.Domain.Enums;

namespace Camply.Domain
{
    /// <summary>
    /// Yorum varlığı
    /// </summary>
    public class Comment : BaseEntity
    {
        /// <summary>
        /// Yorum yapan kullanıcı ID'si
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// Yorum içeriği
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Yorumun durumu
        /// </summary>
        public CommentStatus Status { get; set; }

        /// <summary>
        /// İlişkili varlık ID'si (Post veya Blog)
        /// </summary>
        public Guid EntityId { get; set; }

        /// <summary>
        /// İlişkili varlık türü ("Post" veya "Blog")
        /// </summary>
        public string EntityType { get; set; }

        /// <summary>
        /// Üst yorum ID'si (yanıt için, varsa)
        /// </summary>
        public Guid? ParentId { get; set; }

        // Navigation properties

        /// <summary>
        /// Yorum yapan kullanıcı
        /// </summary>
        public virtual User User { get; set; }

        /// <summary>
        /// Üst yorum (yanıt için)
        /// </summary>
        public virtual Comment Parent { get; set; }

        public Comment()
        {
            Status = CommentStatus.Active;
        }
    }
}
