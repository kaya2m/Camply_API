using Camply.Domain.Auth;
using Camply.Domain.Common;

namespace Camply.Domain
{
    public class CommentLike : BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid CommentId { get; set; }
        
        // Navigation properties
        public User User { get; set; }
        public Comment Comment { get; set; }
    }
}