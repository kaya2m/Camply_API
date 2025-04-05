using Camply.Domain.Auth;
using Camply.Domain.Common;

namespace Camply.Domain;

/// <summary>
/// Takip varlığı
/// </summary>
public class Follow : BaseTrackingEntity
{
    /// <summary>
    /// Takip ID'si
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Takipçi kullanıcı ID'si
    /// </summary>
    public Guid FollowerId { get; set; }

    /// <summary>
    /// Takip edilen kullanıcı ID'si
    /// </summary>
    public Guid FollowedId { get; set; }

    // Navigation properties

    /// <summary>
    /// Takipçi kullanıcı
    /// </summary>
    public virtual User Follower { get; set; }

    /// <summary>
    /// Takip edilen kullanıcı
    /// </summary>
    public virtual User Followed { get; set; }
}
