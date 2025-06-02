using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Media.DTOs
{
    public class MediaUploadResponse
    {
        public Guid Id { get; set; }
        public string FileName { get; set; }
        public string FileType { get; set; }
        public string MimeType { get; set; }
        public string Url { get; set; }
        public string ThumbnailUrl { get; set; }
        public long FileSize { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsTemporary { get; set; }
    }
}
