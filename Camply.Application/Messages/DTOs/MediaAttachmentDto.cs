namespace Camply.Application.Messages.DTOs
{
    public class MediaAttachmentDto
    {
        public string MediaType { get; set; }
        public string Url { get; set; }
        public string ThumbnailUrl { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int Duration { get; set; }
    }
}
