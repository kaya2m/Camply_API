using Camply.Domain.Common;

public class Category : BaseEntity
{
    public string Name { get; set; }
    public string Slug { get; set; }
    public string Description { get; set; }
    public string ImageUrl { get; set; }
    public Guid? ParentId { get; set; }
    public int SortOrder { get; set; }

    // Navigation properties
    public virtual Category Parent { get; set; }
    public virtual ICollection<Category> Children { get; set; }
    public virtual ICollection<BlogCategory> Blogs { get; set; }

    public Category()
    {
        Children = new HashSet<Category>();
        Blogs = new HashSet<BlogCategory>();
    }
}