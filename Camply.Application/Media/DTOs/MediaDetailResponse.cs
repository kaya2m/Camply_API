namespace Camply.Application.Media.DTOs
{
    public class MediaDetailResponse : MediaUploadResponse
    {
        public string Description { get; set; }
        public string AltTag { get; set; }
        public Guid? EntityId { get; set; }
        public string EntityType { get; set; }
        public Guid UserId { get; set; }
        public string Username { get; set; }
    }
}
