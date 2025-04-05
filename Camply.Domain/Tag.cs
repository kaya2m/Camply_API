using Camply.Domain.Common;

public class Tag : BaseEntity
{
    public string Name { get; set; }
    public string Slug { get; set; }
    public int UsageCount { get; set; }

    // Navigation properties
    public virtual ICollection<PostTag> Posts { get; set; }
    public virtual ICollection<BlogTag> Blogs { get; set; }

    public Tag()
    {
        Posts = new HashSet<PostTag>();
        Blogs = new HashSet<BlogTag>();
    }
}