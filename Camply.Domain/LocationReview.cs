using Camply.Domain;
using Camply.Domain.Auth;
using Camply.Domain.Common;
using System.ComponentModel.DataAnnotations;

public class LocationReview : BaseEntity
{
    public Guid LocationId { get; set; }
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; }

    [Required]
    [MaxLength(2000)]
    public string Content { get; set; }

    [Required]
    public ReviewRating OverallRating { get; set; }

    public ReviewRating? CleanlinessRating { get; set; }
    public ReviewRating? ServiceRating { get; set; }
    public ReviewRating? LocationRating { get; set; }
    public ReviewRating? ValueRating { get; set; }
    public ReviewRating? FacilitiesRating { get; set; }

    public bool IsVerified { get; set; }
    public bool IsRecommended { get; set; }

    public DateTime? VisitDate { get; set; }
    public int? StayDuration { get; set; }

    public int HelpfulCount { get; set; }
    public int NotHelpfulCount { get; set; }

    public string OwnerResponse { get; set; }
    public DateTime? OwnerResponseDate { get; set; }

    public virtual Location Location { get; set; }
    public virtual User User { get; set; }
    public virtual ICollection<Media> Photos { get; set; }
    public virtual ICollection<ReviewHelpful> HelpfulVotes { get; set; }

    public LocationReview()
    {
        Photos = new HashSet<Media>();
        HelpfulVotes = new HashSet<ReviewHelpful>();
    }

    public double GetAverageDetailedRating()
    {
        var ratings = new List<int>();
        if (CleanlinessRating.HasValue) ratings.Add((int)CleanlinessRating.Value);
        if (ServiceRating.HasValue) ratings.Add((int)ServiceRating.Value);
        if (LocationRating.HasValue) ratings.Add((int)LocationRating.Value);
        if (ValueRating.HasValue) ratings.Add((int)ValueRating.Value);
        if (FacilitiesRating.HasValue) ratings.Add((int)FacilitiesRating.Value);

        return ratings.Any() ? ratings.Average() : (int)OverallRating;
    }
}