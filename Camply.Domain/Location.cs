using Camply.Domain;
using Camply.Domain.Auth;
using Camply.Domain.Common;
using Camply.Domain.Enums;

public class Location : BaseEntity
{
    public string Name { get; set; }
    public string Description { get; set; }
    public string Address { get; set; }
    public string City { get; set; }
    public string State { get; set; }
    public string Country { get; set; }
    public string PostalCode { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public LocationType Type { get; set; }
    public LocationStatus Status { get; set; }
    public Guid? AddedByUserId { get; set; }
    public bool IsVerified { get; set; }
    public string ContactPhone { get; set; }
    public string ContactEmail { get; set; }
    public string Website { get; set; }
    public string OpeningHours { get; set; }
    public string Amenities { get; set; }
    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }

    // Navigation properties
    public virtual User AddedByUser { get; set; }
    public virtual ICollection<Media> Photos { get; set; }
    public virtual ICollection<LocationReview> Reviews { get; set; }
    public virtual ICollection<Post> Posts { get; set; }
    public virtual ICollection<Blog> Blogs { get; set; }

    public Location()
    {
        Photos = new HashSet<Media>();
        Reviews = new HashSet<LocationReview>();
        Posts = new HashSet<Post>();
        Blogs = new HashSet<Blog>();
    }
}