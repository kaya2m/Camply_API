using Camply.Domain.Auth;
using Camply.Domain.Common;

namespace Camply.Domain
{
    /// <summary>
    /// Beğeni varlığı
    /// </summary>
    public class Like : BaseTrackingEntity
    {
        /// <summary>
        /// Beğeniyi yapan kullanıcı ID'si
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// İlişkili varlık ID'si (Post veya Blog)
        /// </summary>
        public Guid EntityId { get; set; }

        /// <summary>
        /// İlişkili varlık türü ("Post" veya "Blog")
        /// </summary>
        public string EntityType { get; set; }

        // Navigation properties

        /// <summary>
        /// Beğeniyi yapan kullanıcı
        /// </summary>
        public virtual User User { get; set; }
    }
}
