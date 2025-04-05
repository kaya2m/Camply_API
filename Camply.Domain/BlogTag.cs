using Camply.Domain;
using Camply.Domain.Common;

public class BlogTag : BaseTrackingEntity
{
    public Guid BlogId { get; set; }
    public Guid TagId { get; set; }

    // Navigation properties
    public virtual Blog Blog { get; set; }
    public virtual Tag Tag { get; set; }
}