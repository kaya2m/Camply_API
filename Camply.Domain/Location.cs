using Camply.Domain;
using Camply.Domain.Auth;
using Camply.Domain.Common;
using Camply.Domain.Enums;
using System.ComponentModel.DataAnnotations;

public class Location : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; }

    [MaxLength(2000)]
    public string Description { get; set; }

    [MaxLength(500)]
    public string Address { get; set; }

    [MaxLength(100)]
    public string City { get; set; }

    [MaxLength(100)]
    public string State { get; set; }

    [Required]
    [MaxLength(100)]
    public string Country { get; set; }

    [MaxLength(20)]
    public string PostalCode { get; set; }

    [Required]
    public double Latitude { get; set; }

    [Required]
    public double Longitude { get; set; }

    [Required]
    public LocationType Type { get; set; }

    [Required]
    public LocationStatus Status { get; set; }

    public Guid? AddedByUserId { get; set; }

    public bool IsVerified { get; set; }

    // Sponsorluk özellikleri
    public bool IsSponsored { get; set; }
    public DateTime? SponsoredUntil { get; set; }
    public int SponsoredPriority { get; set; } = 0; // 0-10 arası, yüksek öncelik

    // İletişim bilgileri
    [MaxLength(50)]
    public string ContactPhone { get; set; }

    [MaxLength(100)]
    public string ContactEmail { get; set; }

    [MaxLength(500)]
    public string Website { get; set; }

    [MaxLength(1000)]
    public string OpeningHours { get; set; }

    // Özellikler (Flags enum olarak)
    public LocationFeature Features { get; set; } = LocationFeature.None;

    // Giriş ücreti
    public bool HasEntryFee { get; set; }
    public decimal? EntryFee { get; set; }
    public string Currency { get; set; } = "TRY";

    // Rating ve Review bilgileri
    public double? AverageRating { get; set; }
    public int ReviewCount { get; set; }
    public int TotalVisitCount { get; set; }

    // Sosyal medya
    [MaxLength(200)]
    public string FacebookUrl { get; set; }

    [MaxLength(200)]
    public string InstagramUrl { get; set; }

    [MaxLength(200)]
    public string TwitterUrl { get; set; }

    // Kapasite bilgileri (bilgi amaçlı)
    public int? MaxCapacity { get; set; }
    public int? MaxVehicles { get; set; }

    // Onay bilgileri
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string RejectionReason { get; set; }

    // Metadata (JSON)
    public string Metadata { get; set; }

    // Navigation properties
    public virtual User AddedByUser { get; set; }
    public virtual User ApprovedByUser { get; set; }
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

    // Helper methods
    public bool HasFeature(LocationFeature feature)
    {
        return Features.HasFlag(feature);
    }

    public bool IsActive => Status == LocationStatus.Active && !IsDeleted;
    public bool IsSponsoredAndActive => IsActive && IsSponsored &&
        SponsoredUntil.HasValue && SponsoredUntil.Value > DateTime.UtcNow;
}

