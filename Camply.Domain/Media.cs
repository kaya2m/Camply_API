using Camply.Domain.Common;
using Camply.Domain.Enums;

public class Media : BaseEntity
{
    public string FileName { get; set; }
    public string FileType { get; set; }
    public string MimeType { get; set; }
    public string Url { get; set; }
    public string ThumbnailUrl { get; set; }
    public long FileSize { get; set; }
    public MediaType Type { get; set; }
    public string Description { get; set; }
    public string AltTag { get; set; }
    public MediaStatus Status { get; set; }
    public Guid? EntityId { get; set; }
    public string EntityType { get; set; }
    public int? SortOrder { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string Metadata { get; set; }
}
