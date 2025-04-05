using Camply.Domain.Auth;
using Camply.Domain.Common;

public class LocationReview : BaseEntity
{
    public Guid LocationId { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; }
    public string Content { get; set; }
    public double Rating { get; set; } // 1.0 - 5.0
    public bool IsVerified { get; set; }

    // Navigation properties
    public virtual Location Location { get; set; }
    public virtual User User { get; set; }
}