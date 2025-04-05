using Camply.Domain;
using Camply.Domain.Common;

public class BlogCategory : BaseTrackingEntity
{
    public Guid BlogId { get; set; }
    public Guid CategoryId { get; set; }

    // Navigation properties
    public virtual Blog Blog { get; set; }
    public virtual Category Category { get; set; }
}