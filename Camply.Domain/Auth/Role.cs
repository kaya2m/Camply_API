using Camply.Domain.Common;

public class Role : BaseEntity
{
    public string Name { get; set; }
    public string Description { get; set; }

    // Navigation properties
    public virtual ICollection<UserRole> UserRoles { get; set; }

    public Role()
    {
        UserRoles = new HashSet<UserRole>();
    }
}