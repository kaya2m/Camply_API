using Camply.Domain.Auth;
using Camply.Domain.Common;

public class SocialLogin : BaseEntity
{
    public Guid UserId { get; set; }
    public string Provider { get; set; }
    public string ProviderKey { get; set; }
    public string ProviderDisplayName { get; set; }
    public string AccessToken { get; set; }
    public DateTime? TokenExpiry { get; set; }
    public string RefreshToken { get; set; }

    // Navigation properties
    public virtual User User { get; set; }
}