using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Camply.Application.Common.Models;
using Camply.Application.Media.DTOs;
using Camply.Application.Media.Interfaces;
using Camply.Domain.Auth;
using Camply.Domain.Enums;
using Camply.Domain.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Camply.Infrastructure.Services
{
    public class MediaService : IMediaService
    {
        private readonly IRepository<Media> _mediaRepository;
        private readonly IRepository<User> _userRepository;
        private readonly IConfiguration _configuration;
        private readonly ILogger<MediaService> _logger;

        private readonly string[] _allowedImageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private readonly long _maxFileSize;
        private readonly string _uploadDirectory;
        private readonly string _baseUrl;
        private readonly string _rootPath;
        public MediaService(
            IRepository<Media> mediaRepository,
            IRepository<User> userRepository,
            IConfiguration configuration,
            ILogger<MediaService> logger)
        {
            _mediaRepository = mediaRepository;
            _userRepository = userRepository;
            _configuration = configuration;
            _logger = logger;
            _rootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
            // Configuration
            _maxFileSize = configuration.GetValue<long>("Storage:MaxFileSize", 10 * 1024 * 1024); // Default 10MB
            _uploadDirectory = configuration.GetValue<string>("Storage:UploadDirectory", "wwwroot/uploads");
            _baseUrl = configuration.GetValue<string>("Storage:BaseUrl", "/uploads");

          
        }

        public async Task<MediaUploadResponse> UploadMediaAsync(Guid userId, IFormFile file)
        {
            try
            {
                // Validate file
                if (file == null || file.Length == 0)
                {
                    throw new ArgumentException("File is empty");
                }

                if (file.Length > _maxFileSize)
                {
                    throw new ArgumentException($"File size exceeds the limit of {_maxFileSize / (1024 * 1024)} MB");
                }

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!_allowedImageExtensions.Contains(extension))
                {
                    throw new ArgumentException($"File type {extension} is not supported");
                }

                // Check if user exists
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                // Process the file
                using var stream = file.OpenReadStream();

                return await UploadMediaFromStreamAsync(userId, stream, file.FileName, file.ContentType);
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
                var extension = Path.GetExtension(fileName).ToLowerInvariant();

                var uniqueFileName = $"{Guid.NewGuid()}{extension}";
                var relativePath = Path.Combine(_uploadDirectory, uniqueFileName).Replace('\\', '/');
                var filePath = Path.Combine(_rootPath, "media", "uploads", relativePath);

                var mediaType = DetermineMediaType(extension);

                // Save file to disk
                using (var fileStream2 = new FileStream(filePath, FileMode.Create))
                {
                    await fileStream.CopyToAsync(fileStream2);
                }

                // Create thumbnail for images
                string thumbnailPath = null;
                int? width = null;
                int? height = null;

                if (mediaType == MediaType.Image)
                {
                    var thumbnailFileName = $"thumb_{uniqueFileName}";
                    var thumbnailRelativePath = Path.Combine(_uploadDirectory, thumbnailFileName).Replace('\\', '/');
                    thumbnailPath = Path.Combine(_rootPath, "media", "uploads", thumbnailRelativePath);

                    // Get image dimensions and create thumbnail
                    (width, height) = await CreateThumbnailAsync(filePath, thumbnailPath);
                }

                // Create media record
                var media = new Media
                {
                    Id = Guid.NewGuid(),
                    FileName = Path.GetFileName(fileName),
                    FileType = extension,
                    MimeType = contentType,
                    Url = $"{_baseUrl}/{uniqueFileName}",
                    ThumbnailUrl = thumbnailPath != null ? $"{_baseUrl}/thumb_{uniqueFileName}" : null,
                    FileSize = fileStream.Length,
                    Type = mediaType,
                    Width = width,
                    Height = height,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId,
                    Status = MediaStatus.Active
                };

                await _mediaRepository.AddAsync(media);
                await _mediaRepository.SaveChangesAsync();

                // Return response
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
                _logger.LogError(ex, "Error processing uploaded media");
                throw;
            }
        }

        public async Task<MediaDetailResponse> GetMediaByIdAsync(Guid mediaId)
        {
            try
            {
                var media = await _mediaRepository.GetByIdAsync(mediaId);
                if (media == null)
                {
                    throw new KeyNotFoundException($"Media with ID {mediaId} not found");
                }

                var user = await _userRepository.GetByIdAsync(media.CreatedBy.Value);

                return new MediaDetailResponse
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
                // Check if user exists
                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                // Get media by creator
                var query = await _mediaRepository.FindAsync(m => m.CreatedBy == userId);
                var mediaList = query.ToList();

                // Apply sorting (newest first)
                mediaList = mediaList.OrderByDescending(m => m.CreatedAt).ToList();

                // Get total count
                var totalCount = mediaList.Count;

                // Apply pagination
                var paginatedMedia = mediaList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var mediaResponses = paginatedMedia.Select(m => new MediaSummaryResponse
                {
                    Id = m.Id,
                    Url = m.Url,
                    ThumbnailUrl = m.ThumbnailUrl,
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
                // Get media by entity
                var query = await _mediaRepository.FindAsync(m => m.EntityId == entityId && m.EntityType == entityType);
                var mediaList = query.ToList();

                // Apply sorting (by sort order if available, otherwise by created date)
                mediaList = mediaList
                    .OrderBy(m => m.SortOrder ?? int.MaxValue)
                    .ThenByDescending(m => m.CreatedAt)
                    .ToList();

                // Get total count
                var totalCount = mediaList.Count;

                // Apply pagination
                var paginatedMedia = mediaList
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                // Map to response
                var mediaResponses = paginatedMedia.Select(m => new MediaSummaryResponse
                {
                    Id = m.Id,
                    Url = m.Url,
                    ThumbnailUrl = m.ThumbnailUrl,
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
                {
                    throw new KeyNotFoundException($"Media with ID {mediaId} not found");
                }

                // Check if user is the creator
                if (media.CreatedBy != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to delete this media");
                }

                // Delete files from disk
                var filePath = Path.Combine(_rootPath, "media", "uploads", media.Url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // Delete thumbnail if exists
                if (!string.IsNullOrEmpty(media.ThumbnailUrl))
                {
                    var thumbnailPath = Path.Combine(_rootPath, "media", "uploads", media.ThumbnailUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(thumbnailPath))
                    {
                        File.Delete(thumbnailPath);
                    }
                }

                // Delete record
                _mediaRepository.Remove(media);
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
                {
                    throw new KeyNotFoundException($"Media with ID {mediaId} not found");
                }

                // Check if user is the creator
                if (media.CreatedBy != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to update this media");
                }

                // Update details
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
                {
                    throw new KeyNotFoundException($"Media with ID {mediaId} not found");
                }

                // Check if user is the creator
                if (media.CreatedBy != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to update this media");
                }

                // Assign to entity
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
                {
                    throw new KeyNotFoundException($"Media with ID {mediaId} not found");
                }

                // Check if user is the creator
                if (media.CreatedBy != userId)
                {
                    throw new UnauthorizedAccessException("You are not authorized to update this media");
                }

                // Unassign from entity
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

        private MediaType DetermineMediaType(string extension)
        {
            switch (extension.ToLower())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".webp":
                    return MediaType.Image;
                case ".mp4":
                case ".mov":
                case ".avi":
                case ".wmv":
                    return MediaType.Video;
                case ".mp3":
                case ".wav":
                case ".ogg":
                    return MediaType.Audio;
                case ".pdf":
                case ".doc":
                case ".docx":
                case ".xls":
                case ".xlsx":
                case ".ppt":
                case ".pptx":
                    return MediaType.Document;
                default:
                    return MediaType.Image; // Default to image
            }
        }

        private async Task<(int width, int height)> CreateThumbnailAsync(string sourcePath, string targetPath)
        {
            try
            {
                using var image = await Image.LoadAsync(sourcePath);

                int originalWidth = image.Width;
                int originalHeight = image.Height;

                int maxDimension = 300;
                double ratio = (double)originalWidth / originalHeight;

                int thumbnailWidth = originalWidth > originalHeight
                    ? maxDimension
                    : (int)(maxDimension * ratio);

                int thumbnailHeight = originalWidth > originalHeight
                    ? (int)(maxDimension / ratio)
                    : maxDimension;

                image.Mutate(x => x.Resize(thumbnailWidth, thumbnailHeight));

                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                await image.SaveAsync(targetPath); 

                return (originalWidth, originalHeight);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating thumbnail from {SourcePath} to {TargetPath}", sourcePath, targetPath);
                throw;
            }
        }


        #endregion
    }
}
