using Camply.Domain;
using Camply.Domain.Common;

public class PostTag : BaseTrackingEntity
{
    public Guid PostId { get; set; }
    public Guid TagId { get; set; }

    // Navigation properties
    public virtual Post Post { get; set; }
    public virtual Tag Tag { get; set; }
}