using Camply.Domain.Auth;
using Camply.Domain.Common;

public class UserRole : BaseTrackingEntity
{
    public Guid UserId { get; set; }
    public Guid RoleId { get; set; }

    // Navigation properties
    public virtual User User { get; set; }
    public virtual Role Role { get; set; }
}