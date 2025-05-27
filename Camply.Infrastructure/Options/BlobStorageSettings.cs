using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Options
{
    public class BlobStorageSettings
    {
        public string ConnectionString { get; set; }
        public string ContainerName { get; set; } = "camply-media";
        public string CdnEndpoint { get; set; }
        public bool UseHttps { get; set; } = true;
        public int MaxFileSizeMB { get; set; } = 10;
        public string[] AllowedFileTypes { get; set; } =
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp",
            ".mp4", ".mov", ".avi", ".wmv",
            ".mp3", ".wav", ".ogg",
            ".pdf", ".doc", ".docx"
        };

        // Image resize configurations
        public ImageSizeConfig ProfileImage { get; set; } = new()
        {
            Small = new ImageSize { Width = 150, Height = 150 },
            Medium = new ImageSize { Width = 300, Height = 300 },
            Large = new ImageSize { Width = 600, Height = 600 }
        };

        public ImageSizeConfig PostImage { get; set; } = new()
        {
            Small = new ImageSize { Width = 300, Height = 300 },
            Medium = new ImageSize { Width = 800, Height = 600 },
            Large = new ImageSize { Width = 1200, Height = 900 }
        };

        public ImageSizeConfig BlogImage { get; set; } = new()
        {
            Small = new ImageSize { Width = 400, Height = 300 },
            Medium = new ImageSize { Width = 800, Height = 600 },
            Large = new ImageSize { Width = 1200, Height = 900 }
        };
    }

    public class ImageSizeConfig
    {
        public ImageSize Small { get; set; }
        public ImageSize Medium { get; set; }
        public ImageSize Large { get; set; }
    }

    public class ImageSize
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }
}