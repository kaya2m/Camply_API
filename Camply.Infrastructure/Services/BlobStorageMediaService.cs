using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Camply.Application.Media.DTOs;
using Camply.Application.Media.Interfaces;
using Camply.Domain.Auth;
using Camply.Domain.Enums;
using Camply.Domain.Repositories;
using Camply.Infrastructure.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Camply.Application.Common.Models;

namespace Camply.Infrastructure.Services
{
    public class BlobStorageMediaService : IMediaService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly BlobContainerClient _containerClient;
        private readonly IRepository<Media> _mediaRepository;
        private readonly IRepository<User> _userRepository;
        private readonly BlobStorageSettings _settings;
        private readonly ICloudflareService _cloudflareService;
        private readonly ILogger<BlobStorageMediaService> _logger;

        private readonly string[] _imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private readonly string[] _videoExtensions = { ".mp4", ".mov", ".avi", ".wmv", ".mkv" };
        private readonly string[] _audioExtensions = { ".mp3", ".wav", ".ogg", ".m4a" };

        public BlobStorageMediaService(
            IOptions<BlobStorageSettings> blobSettings,
            IRepository<Media> mediaRepository,
            IRepository<User> userRepository,
            ICloudflareService cloudflareService,
            ILogger<BlobStorageMediaService> logger)
        {
            _settings = blobSettings.Value;
            _mediaRepository = mediaRepository;
            _userRepository = userRepository;
            _cloudflareService = cloudflareService;
            _logger = logger;

            _blobServiceClient = new BlobServiceClient(_settings.ConnectionString);
            _containerClient = _blobServiceClient.GetBlobContainerClient(_settings.ContainerName);
        }

        public async Task<MediaUploadResponse> UploadMediaAsync(Guid userId, IFormFile file)
        {
            try
            {
                await ValidateFile(file);

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    throw new KeyNotFoundException($"User with ID {userId} not found");

                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var mediaType = DetermineMediaType(fileExtension);
                var fileName = GenerateUniqueFileName(fileExtension);

                // Create folder structure: {mediaType}/{year}/{month}/{fileName}
                var folderPath = $"{mediaType.ToString().ToLower()}/{DateTime.UtcNow:yyyy/MM}";
                var blobName = $"{folderPath}/{fileName}";

                using var stream = file.OpenReadStream();

                // Upload original file
                var blobClient = _containerClient.GetBlobClient(blobName);
                var uploadResult = await blobClient.UploadAsync(stream, new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                });

                // Create media record
                var media = new Media
                {
                    Id = Guid.NewGuid(),
                    FileName = Path.GetFileNameWithoutExtension(file.FileName),
                    FileType = fileExtension,
                    MimeType = file.ContentType,
                    Url = GetBlobUrl(blobName),
                    FileSize = file.Length,
                    Type = mediaType,
                    Status = MediaStatus.Processing,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                // Process image variants if it's an image
                if (mediaType == MediaType.Image)
                {
                    stream.Position = 0;
                    var imageData = await ProcessImageVariants(stream, blobName, folderPath);
                    media.Width = imageData.Width;
                    media.Height = imageData.Height;
                    media.ThumbnailUrl = imageData.ThumbnailUrl;
                }

                media.Status = MediaStatus.Active;

                await _mediaRepository.AddAsync(media);
                await _mediaRepository.SaveChangesAsync();

                return new MediaUploadResponse
                {
                    Id = media.Id,
                    FileName = media.FileName,
                    FileType = media.FileType,
                    MimeType = media.MimeType,
                    Url = media.Url,
                    ThumbnailUrl = media.ThumbnailUrl,
                    FileSize = media.FileSize,
                    Width = media.Width,
                    Height = media.Height,
                    CreatedAt = media.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading media file");
                throw;
            }
        }

        public async Task<MediaUploadResponse> UploadMediaFromStreamAsync(Guid userId, Stream fileStream, string fileName, string contentType)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    throw new KeyNotFoundException($"User with ID {userId} not found");

                var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
                await ValidateFileExtension(fileExtension);

                var mediaType = DetermineMediaType(fileExtension);
                var uniqueFileName = GenerateUniqueFileName(fileExtension);

                var folderPath = $"{mediaType.ToString().ToLower()}/{DateTime.UtcNow:yyyy/MM}";
                var blobName = $"{folderPath}/{uniqueFileName}";

                // Upload to blob storage
                var blobClient = _containerClient.GetBlobClient(blobName);
                await blobClient.UploadAsync(fileStream, new BlobHttpHeaders
                {
                    ContentType = contentType
                });

                // Create media record
                var media = new Media
                {
                    Id = Guid.NewGuid(),
                    FileName = Path.GetFileNameWithoutExtension(fileName),
                    FileType = fileExtension,
                    MimeType = contentType,
                    Url = GetBlobUrl(blobName),
                    FileSize = fileStream.Length,
                    Type = mediaType,
                    Status = MediaStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                // Process image variants if it's an image
                if (mediaType == MediaType.Image)
                {
                    fileStream.Position = 0;
                    var imageData = await ProcessImageVariants(fileStream, blobName, folderPath);
                    media.Width = imageData.Width;
                    media.Height = imageData.Height;
                    media.ThumbnailUrl = imageData.ThumbnailUrl;
                }

                await _mediaRepository.AddAsync(media);
                await _mediaRepository.SaveChangesAsync();

                return new MediaUploadResponse
                {
                    Id = media.Id,
                    FileName = media.FileName,
                    FileType = media.FileType,
                    MimeType = media.MimeType,
                    Url = media.Url,
                    ThumbnailUrl = media.ThumbnailUrl,
                    FileSize = media.FileSize,
                    Width = media.Width,
                    Height = media.Height,
                    CreatedAt = media.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading media from stream");
                throw;
            }
        }

        public async Task<MediaDetailResponse> GetMediaByIdAsync(Guid mediaId)
        {
            try
            {
                var media = await _mediaRepository.GetByIdAsync(mediaId);
                if (media == null)
                    throw new KeyNotFoundException($"Media with ID {mediaId} not found");

                var user = await _userRepository.GetByIdAsync(media.CreatedBy.Value);

                return new MediaDetailResponse
                {
                    Id = media.Id,
                    FileName = media.FileName,
                    FileType = media.FileType,
                    MimeType = media.MimeType,
                    Url = GenerateSignedUrl(media.Url),
                    ThumbnailUrl = !string.IsNullOrEmpty(media.ThumbnailUrl) ? GenerateSignedUrl(media.ThumbnailUrl) : null,
                    FileSize = media.FileSize,
                    Width = media.Width,
                    Height = media.Height,
                    Description = media.Description,
                    AltTag = media.AltTag,
                    EntityId = media.EntityId,
                    EntityType = media.EntityType,
                    UserId = media.CreatedBy.Value,
                    Username = user?.Username,
                    CreatedAt = media.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting media with ID {mediaId}");
                throw;
            }
        }

        public async Task<PagedResponse<MediaSummaryResponse>> GetMediaByUserAsync(Guid userId, int pageNumber, int pageSize)
        {
            try
            {
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                    throw new KeyNotFoundException($"User with ID {userId} not found");

                var query = await _mediaRepository.FindAsync(m => m.CreatedBy == userId && m.Status == MediaStatus.Active);
                var mediaList = query.OrderByDescending(m => m.CreatedAt).ToList();

                var totalCount = mediaList.Count;
                var paginatedMedia = mediaList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var mediaResponses = paginatedMedia.Select(m => new MediaSummaryResponse
                {
                    Id = m.Id,
                    Url = GenerateSignedUrl(m.Url),
                    ThumbnailUrl = !string.IsNullOrEmpty(m.ThumbnailUrl) ? GenerateSignedUrl(m.ThumbnailUrl) : null,
                    FileType = m.FileType,
                    Description = m.Description,
                    AltTag = m.AltTag,
                    Width = m.Width,
                    Height = m.Height
                }).ToList();

                return new PagedResponse<MediaSummaryResponse>
                {
                    Items = mediaResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting media for user ID {userId}");
                throw;
            }
        }

        public async Task<PagedResponse<MediaSummaryResponse>> GetMediaByEntityAsync(Guid entityId, string entityType, int pageNumber, int pageSize)
        {
            try
            {
                var query = await _mediaRepository.FindAsync(m =>
                    m.EntityId == entityId &&
                    m.EntityType == entityType &&
                    m.Status == MediaStatus.Active);

                var mediaList = query.OrderBy(m => m.SortOrder ?? int.MaxValue)
                    .ThenBy(m => m.CreatedAt).ToList();

                var totalCount = mediaList.Count;
                var paginatedMedia = mediaList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                var mediaResponses = paginatedMedia.Select(m => new MediaSummaryResponse
                {
                    Id = m.Id,
                    Url = GenerateSignedUrl(m.Url),
                    ThumbnailUrl = !string.IsNullOrEmpty(m.ThumbnailUrl) ? GenerateSignedUrl(m.ThumbnailUrl) : null,
                    FileType = m.FileType,
                    Description = m.Description,
                    AltTag = m.AltTag,
                    Width = m.Width,
                    Height = m.Height
                }).ToList();

                return new PagedResponse<MediaSummaryResponse>
                {
                    Items = mediaResponses,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting media for entity ID {entityId} type {entityType}");
                throw;
            }
        }

        public async Task<bool> DeleteMediaAsync(Guid mediaId, Guid userId)
        {
            try
            {
                var media = await _mediaRepository.GetByIdAsync(mediaId);
                if (media == null)
                    throw new KeyNotFoundException($"Media with ID {mediaId} not found");

                if (media.CreatedBy != userId)
                    throw new UnauthorizedAccessException("You are not authorized to delete this media");

                // Delete from blob storage
                var blobName = ExtractBlobNameFromUrl(media.Url);
                var blobClient = _containerClient.GetBlobClient(blobName);
                await blobClient.DeleteIfExistsAsync();

                // Delete thumbnail if exists
                if (!string.IsNullOrEmpty(media.ThumbnailUrl))
                {
                    var thumbnailBlobName = ExtractBlobNameFromUrl(media.ThumbnailUrl);
                    var thumbnailBlobClient = _containerClient.GetBlobClient(thumbnailBlobName);
                    await thumbnailBlobClient.DeleteIfExistsAsync();
                }

                // Soft delete in database
                media.IsDeleted = true;
                media.DeletedAt = DateTime.UtcNow;
                media.DeletedBy = userId;
                media.Status = MediaStatus.Failed;

                _mediaRepository.Update(media);
                await _mediaRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting media with ID {mediaId}");
                throw;
            }
        }

        public async Task<bool> UpdateMediaDetailsAsync(Guid mediaId, Guid userId, UpdateMediaRequest request)
        {
            try
            {
                var media = await _mediaRepository.GetByIdAsync(mediaId);
                if (media == null)
                    throw new KeyNotFoundException($"Media with ID {mediaId} not found");

                if (media.CreatedBy != userId)
                    throw new UnauthorizedAccessException("You are not authorized to update this media");

                media.Description = request.Description;
                media.AltTag = request.AltTag;
                media.LastModifiedAt = DateTime.UtcNow;
                media.LastModifiedBy = userId;

                _mediaRepository.Update(media);
                await _mediaRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating media details for ID {mediaId}");
                throw;
            }
        }

        public async Task<bool> AssignMediaToEntityAsync(Guid mediaId, Guid entityId, string entityType, Guid userId)
        {
            try
            {
                var media = await _mediaRepository.GetByIdAsync(mediaId);
                if (media == null)
                    throw new KeyNotFoundException($"Media with ID {mediaId} not found");

                if (media.CreatedBy != userId)
                    throw new UnauthorizedAccessException("You are not authorized to update this media");

                media.EntityId = entityId;
                media.EntityType = entityType;
                media.LastModifiedAt = DateTime.UtcNow;
                media.LastModifiedBy = userId;

                _mediaRepository.Update(media);
                await _mediaRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error assigning media ID {mediaId} to entity ID {entityId} type {entityType}");
                throw;
            }
        }

        public async Task<bool> UnassignMediaFromEntityAsync(Guid mediaId, Guid userId)
        {
            try
            {
                var media = await _mediaRepository.GetByIdAsync(mediaId);
                if (media == null)
                    throw new KeyNotFoundException($"Media with ID {mediaId} not found");

                if (media.CreatedBy != userId)
                    throw new UnauthorizedAccessException("You are not authorized to update this media");

                media.EntityId = null;
                media.EntityType = null;
                media.LastModifiedAt = DateTime.UtcNow;
                media.LastModifiedBy = userId;

                _mediaRepository.Update(media);
                await _mediaRepository.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error unassigning media ID {mediaId} from entity");
                throw;
            }
        }

        #region Helper Methods

        private async Task ValidateFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
                throw new ArgumentException("File is empty");

            if (file.Length > _settings.MaxFileSizeMB * 1024 * 1024)
                throw new ArgumentException($"File size exceeds the limit of {_settings.MaxFileSizeMB} MB");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            await ValidateFileExtension(extension);
        }

        private async Task ValidateFileExtension(string extension)
        {
            if (!_settings.AllowedFileTypes.Contains(extension))
                throw new ArgumentException($"File type {extension} is not supported");

            await Task.CompletedTask;
        }

        private MediaType DetermineMediaType(string extension)
        {
            if (_imageExtensions.Contains(extension.ToLower()))
                return MediaType.Image;
            if (_videoExtensions.Contains(extension.ToLower()))
                return MediaType.Video;
            if (_audioExtensions.Contains(extension.ToLower()))
                return MediaType.Audio;

            return MediaType.Document;
        }

        private string GenerateUniqueFileName(string extension)
        {
            return $"{Guid.NewGuid()}{extension}";
        }

        private string GetBlobUrl(string blobName)
        {
            // Always return the direct Azure Blob URL
            // Cloudflare CDN conversion will be handled in GetOptimizedUrl method
            return _containerClient.GetBlobClient(blobName).Uri.ToString();
        }

        private async Task<string> GetOptimizedUrl(string blobUrl, int? width = null, int? height = null, string format = null)
        {
            try
            {
                return await _cloudflareService.GetOptimizedImageUrl(blobUrl, width, height, format);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get optimized URL for {BlobUrl}, returning original", blobUrl);
                return blobUrl;
            }
        }

        private string GenerateSignedUrl(string blobUrl)
        {
            try
            {
                var blobName = ExtractBlobNameFromUrl(blobUrl);
                var blobClient = _containerClient.GetBlobClient(blobName);

                if (blobClient.CanGenerateSasUri)
                {
                    var sasBuilder = new BlobSasBuilder
                    {
                        BlobContainerName = _settings.ContainerName,
                        BlobName = blobName,
                        Resource = "b",
                        ExpiresOn = DateTimeOffset.UtcNow.AddHours(1)
                    };
                    sasBuilder.SetPermissions(BlobSasPermissions.Read);

                    return blobClient.GenerateSasUri(sasBuilder).ToString();
                }

                return blobUrl;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Could not generate signed URL for {blobUrl}");
                return blobUrl;
            }
        }

        private string ExtractBlobNameFromUrl(string url)
        {
            var uri = new Uri(url);
            return uri.Segments.Skip(2).Aggregate((a, b) => a + b).TrimEnd('/');
        }

        private async Task<(int Width, int Height, string ThumbnailUrl)> ProcessImageVariants(Stream imageStream, string originalBlobName, string folderPath)
        {
            try
            {
                using var image = await Image.LoadAsync(imageStream);
                var originalWidth = image.Width;
                var originalHeight = image.Height;

                // Create thumbnail (300x300)
                var thumbnailSize = 300;
                using var thumbnail = image.Clone(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(thumbnailSize, thumbnailSize),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));

                // Generate thumbnail blob name
                var thumbnailBlobName = $"{folderPath}/thumbnails/{Path.GetFileNameWithoutExtension(originalBlobName)}_thumb.jpg";

                // Upload thumbnail
                using var thumbnailStream = new MemoryStream();
                await thumbnail.SaveAsJpegAsync(thumbnailStream, new JpegEncoder { Quality = 85 });
                thumbnailStream.Position = 0;

                var thumbnailBlobClient = _containerClient.GetBlobClient(thumbnailBlobName);
                await thumbnailBlobClient.UploadAsync(thumbnailStream, new BlobHttpHeaders
                {
                    ContentType = "image/jpeg"
                });

                return (originalWidth, originalHeight, GetBlobUrl(thumbnailBlobName));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image variants");
                return (0, 0, null);
            }
        }

        #endregion
    }
}