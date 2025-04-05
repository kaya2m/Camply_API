using Camply.Domain.Auth;
using Camply.Domain.Common;
using Camply.Domain.Enums;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; }
    public string Message { get; set; }
    public bool IsRead { get; set; }
    public Guid? ActorId { get; set; }
    public Guid? EntityId { get; set; }
    public string EntityType { get; set; }
    public DateTime? ReadAt { get; set; }

    // Navigation properties
    public virtual User User { get; set; }
    public virtual User Actor { get; set; }
}