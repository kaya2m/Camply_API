using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Camply.Application.Common.Models;
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
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Text;

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

            try
            {
                _blobServiceClient = new BlobServiceClient(_settings.ConnectionString);
                _containerClient = _blobServiceClient.GetBlobContainerClient(_settings.ContainerName);

                _logger.LogInformation("BlobStorageMediaService initialized successfully with container: {ContainerName}", _settings.ContainerName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize BlobStorageMediaService");
                throw;
            }
        }

        public async Task<MediaUploadResponse> UploadMediaAsync(Guid userId, IFormFile file)
        {
            try
            {
                await ValidateFile(file);
                _logger.LogInformation("File validation passed for user {UserId}, file: {FileName}", userId, file.FileName);

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    _logger.LogWarning("User not found with ID: {UserId}", userId);
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var mediaType = DetermineMediaType(fileExtension);
                var fileName = GenerateUniqueFileName(fileExtension);

                var folderPath = $"{mediaType.ToString().ToLowerInvariant()}/{DateTime.UtcNow:yyyy/MM}";
                var blobName = $"{folderPath}/{fileName}";

                _logger.LogInformation("Uploading file to blob: {BlobName}", blobName);

                using var stream = file.OpenReadStream();

                await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                var blobClient = _containerClient.GetBlobClient(blobName);

                var blobHttpHeaders = new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                };
                var sanitizedOriginalFileName = SanitizeForAscii(file.FileName);

                var metadata = new Dictionary<string, string>
                {
                    ["OriginalFileName"] = sanitizedOriginalFileName,
                    ["UploadedBy"] = userId.ToString(),
                    ["UploadedAt"] = DateTime.UtcNow.ToString("O"),
                    ["MediaType"] = mediaType.ToString()
                };

                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders,
                    Metadata = metadata
                };

                var uploadResult = await blobClient.UploadAsync(stream, uploadOptions);

                if (uploadResult == null)
                {
                    throw new InvalidOperationException("Failed to upload file to blob storage");
                }

                _logger.LogInformation("File uploaded successfully to blob: {BlobName}", blobName);

                var media = new Media
                {
                    Id = Guid.NewGuid(),
                    FileName = Path.GetFileNameWithoutExtension(file.FileName),
                    FileType = fileExtension,
                    MimeType = file.ContentType,
                    Url = blobClient.Uri.ToString(),
                    FileSize = file.Length,
                    Type = mediaType,
                    Status = MediaStatus.Processing,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                if (mediaType == MediaType.Image)
                {
                    try
                    {
                        stream.Position = 0;
                        var imageData = await ProcessImageVariants(stream, blobName, folderPath);
                        media.Width = imageData.Width;
                        media.Height = imageData.Height;
                        media.ThumbnailUrl = imageData.ThumbnailUrl;

                        _logger.LogInformation("Image variants processed successfully for {BlobName}", blobName);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process image variants for {BlobName}", blobName);
                    }
                }

                media.Status = MediaStatus.Active;

                await _mediaRepository.AddAsync(media);
                await _mediaRepository.SaveChangesAsync();

                _logger.LogInformation("Media record created successfully with ID: {MediaId}", media.Id);

                user.ProfileImageUrl = GenerateSecureUrl(media.Url);
                _userRepository.Update(user);
                await _userRepository.SaveChangesAsync();

                return new MediaUploadResponse
                {
                    Id = media.Id,
                    FileName = media.FileName,
                    FileType = media.FileType,
                    MimeType = media.MimeType,
                    Url = GenerateSecureUrl(media.Url), // SAS token ile
                    ThumbnailUrl = !string.IsNullOrEmpty(media.ThumbnailUrl)
                            ? GenerateSecureUrl(media.ThumbnailUrl)
                            : null,
                    FileSize = media.FileSize,
                    Width = media.Width,
                    Height = media.Height,
                    CreatedAt = media.CreatedAt
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading media file for user {UserId}", userId);
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

                // Ensure container exists
                await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                // Upload to blob storage
                var blobClient = _containerClient.GetBlobClient(blobName);

                var blobHttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                };

                var metadata = new Dictionary<string, string>
                {
                    ["OriginalFileName"] = fileName,
                    ["UploadedBy"] = userId.ToString(),
                    ["UploadedAt"] = DateTime.UtcNow.ToString("O"),
                    ["MediaType"] = mediaType.ToString()
                };

                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders,
                    Metadata = metadata
                };

                await blobClient.UploadAsync(fileStream, uploadOptions);

                // Create media record
                var media = new Media
                {
                    Id = Guid.NewGuid(),
                    FileName = Path.GetFileNameWithoutExtension(fileName),
                    FileType = fileExtension,
                    MimeType = contentType,
                    Url = blobClient.Uri.ToString(),
                    FileSize = fileStream.Length,
                    Type = mediaType,
                    Status = MediaStatus.Active,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = userId
                };

                // Process image variants if it's an image
                if (mediaType == MediaType.Image)
                {
                    try
                    {
                        fileStream.Position = 0;
                        var imageData = await ProcessImageVariants(fileStream, blobName, folderPath);
                        media.Width = imageData.Width;
                        media.Height = imageData.Height;
                        media.ThumbnailUrl = imageData.ThumbnailUrl;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process image variants");
                        // Continue without thumbnails
                    }
                }

                await _mediaRepository.AddAsync(media);
                await _mediaRepository.SaveChangesAsync();

                return new MediaUploadResponse
                {
                    Id = media.Id,
                    FileName = media.FileName,
                    FileType = media.FileType,
                    MimeType = media.MimeType,
                    Url = GenerateSecureUrl(media.Url), // SAS token ile
                    ThumbnailUrl = !string.IsNullOrEmpty(media.ThumbnailUrl)
                            ? GenerateSecureUrl(media.ThumbnailUrl)
                            : null,
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
                    Url = GenerateSecureUrl(media.Url), // SAS token ile güvenli URL
                    ThumbnailUrl = !string.IsNullOrEmpty(media.ThumbnailUrl)
                        ? GenerateSecureUrl(media.ThumbnailUrl)
                        : null,
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

                var mediaResponses = new List<MediaSummaryResponse>();
                foreach (var m in paginatedMedia)
                {
                    mediaResponses.Add(new MediaSummaryResponse
                    {
                        Id = m.Id,
                        Url = GenerateSecureUrl(m.Url), // SAS token ile
                        ThumbnailUrl = !string.IsNullOrEmpty(m.ThumbnailUrl)
                            ? GenerateSecureUrl(m.ThumbnailUrl)
                            : null,
                        FileType = m.FileType,
                        Description = m.Description,
                        AltTag = m.AltTag,
                        Width = m.Width,
                        Height = m.Height
                    });
                }

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

                var mediaResponses = new List<MediaSummaryResponse>();
                foreach (var m in paginatedMedia)
                {
                    mediaResponses.Add(new MediaSummaryResponse
                    {
                        Id = m.Id,
                        Url = GenerateSecureUrl(m.Url), // SAS token ile
                        ThumbnailUrl = !string.IsNullOrEmpty(m.ThumbnailUrl)
                            ? GenerateSecureUrl(m.ThumbnailUrl)
                            : null,
                        FileType = m.FileType,
                        Description = m.Description,
                        AltTag = m.AltTag,
                        Width = m.Width,
                        Height = m.Height
                    });
                }

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

                var blobName = ExtractBlobNameFromUrl(media.Url);
                var blobClient = _containerClient.GetBlobClient(blobName);

                var deleteResult = await blobClient.DeleteIfExistsAsync();
                if (deleteResult.Value)
                {
                    _logger.LogInformation("Successfully deleted blob: {BlobName}", blobName);
                }

                if (!string.IsNullOrEmpty(media.ThumbnailUrl))
                {
                    var thumbnailBlobName = ExtractBlobNameFromUrl(media.ThumbnailUrl);
                    var thumbnailBlobClient = _containerClient.GetBlobClient(thumbnailBlobName);
                    await thumbnailBlobClient.DeleteIfExistsAsync();
                }

                // Purge from Cloudflare cache
                try
                {
                    await _cloudflareService.PurgeCache(media.Url);
                    if (!string.IsNullOrEmpty(media.ThumbnailUrl))
                    {
                        await _cloudflareService.PurgeCache(media.ThumbnailUrl);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to purge cache for deleted media {MediaId}", mediaId);
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
        public async Task<List<MediaUploadResponse>> UploadPostMediaAsync(Guid userId, List<IFormFile> files)
        {
            try
            {
                _logger.LogInformation("Starting temporary media upload for user {UserId}, file count: {FileCount}",
                    userId, files.Count);

                var results = new List<MediaUploadResponse>();

                var validationResult = await ValidateMediaFilesAsync(files);
                if (validationResult.ErrorMessage is not null)
                {
                    throw new ArgumentException(validationResult.ErrorMessage);
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                foreach (var file in files)
                {
                    try
                    {
                        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        var mediaType = DetermineMediaType(fileExtension);
                        var fileName = GenerateUniqueFileName(fileExtension);

                        var folderPath = $"post/{userId}/{DateTime.UtcNow:yyyy/MM/dd}";
                        var blobName = $"{folderPath}/{fileName}";

                        _logger.LogInformation("Uploading temporary file to blob: {BlobName}", blobName);

                        using var stream = file.OpenReadStream();

                        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

                        var blobClient = _containerClient.GetBlobClient(blobName);

                        var blobHttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = file.ContentType
                        };

                        var sanitizedOriginalFileName = SanitizeForAscii(file.FileName);

                        var metadata = new Dictionary<string, string>
                        {
                            ["OriginalFileName"] = sanitizedOriginalFileName,
                            ["UploadedBy"] = userId.ToString(),
                            ["UploadedAt"] = DateTime.UtcNow.ToString("O"),
                            ["MediaType"] = mediaType.ToString(),
                            ["IsTemporary"] = "true",
                            ["ExpiresAt"] = DateTime.UtcNow.AddHours(24).ToString("O")
                        };

                        var uploadOptions = new BlobUploadOptions
                        {
                            HttpHeaders = blobHttpHeaders,
                            Metadata = metadata
                        };

                        await blobClient.UploadAsync(stream, uploadOptions);

                        var media = new Media
                        {
                            Id = Guid.NewGuid(),
                            FileName = Path.GetFileNameWithoutExtension(file.FileName),
                            FileType = fileExtension,
                            MimeType = file.ContentType,
                            Url = blobClient.Uri.ToString(),
                            FileSize = file.Length,
                            Type = mediaType,
                            Status = MediaStatus.Processing,
                            EntityId = null,
                            EntityType = "Post",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = userId,
                            Metadata = "{\"isTemporary\": true, \"expiresAt\": \"" + DateTime.UtcNow.AddHours(24).ToString("O") + "\", \"originalFileName\": \"" + EscapeJsonString(file.FileName) + "\"}"
                        };

                        if (mediaType == MediaType.Image)
                        {
                            try
                            {
                                stream.Position = 0;
                                var imageData = await ProcessImageVariants(stream, blobName, folderPath);
                                media.Width = imageData.Width;
                                media.Height = imageData.Height;
                                media.ThumbnailUrl = imageData.ThumbnailUrl;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to process image variants for temporary media {BlobName}", blobName);
                            }
                        }

                        media.Status = MediaStatus.Active;

                        await _mediaRepository.AddAsync(media);
                        await _mediaRepository.SaveChangesAsync();

                        results.Add(new MediaUploadResponse
                        {
                            Id = media.Id,
                            FileName = media.FileName,
                            FileType = media.FileType,
                            MimeType = media.MimeType,
                            Url = GenerateSecureUrl(media.Url),
                            ThumbnailUrl = !string.IsNullOrEmpty(media.ThumbnailUrl)
                                ? GenerateSecureUrl(media.ThumbnailUrl)
                                : null,
                            FileSize = media.FileSize,
                            Width = media.Width,
                            Height = media.Height,
                            CreatedAt = media.CreatedAt,
                            IsTemporary = true
                        });

                        _logger.LogInformation("Temporary media uploaded successfully: {MediaId}", media.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading temporary file: {FileName}", SanitizeForLogging(file.FileName));
                        throw;
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in temporary media upload for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<MediaUploadResponse>> UploadLocationMediaAsync(Guid userId, List<IFormFile> files)
        {
            try
            {
                _logger.LogInformation("Starting temporary media upload for user {UserId}, file count: {FileCount}",
                    userId, files.Count);

                var results = new List<MediaUploadResponse>();

                var validationResult = await ValidateMediaFilesAsync(files);
                if (validationResult.ErrorMessage is not null)
                {
                    throw new ArgumentException(validationResult.ErrorMessage);
                }

                var user = await _userRepository.GetByIdAsync(userId);
                if (user == null)
                {
                    throw new KeyNotFoundException($"User with ID {userId} not found");
                }

                foreach (var file in files)
                {
                    try
                    {
                        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                        var mediaType = DetermineMediaType(fileExtension);
                        var fileName = GenerateUniqueFileName(fileExtension);

                        var folderPath = $"location/{userId}/{DateTime.UtcNow:yyyy/MM/dd}";
                        var blobName = $"{folderPath}/{fileName}";

                        using var stream = file.OpenReadStream();

                        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
                        var blobClient = _containerClient.GetBlobClient(blobName);
                        var blobHttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = file.ContentType
                        };
                        var sanitizedOriginalFileName = SanitizeForAscii(file.FileName);
                        var metadata = new Dictionary<string, string>
                        {
                            ["OriginalFileName"] = sanitizedOriginalFileName,
                            ["UploadedBy"] = userId.ToString(),
                            ["UploadedAt"] = DateTime.UtcNow.ToString("O"),
                            ["MediaType"] = mediaType.ToString(),
                            ["IsTemporary"] = "false",
                            ["ExpiresAt"] = DateTime.UtcNow.AddHours(24).ToString("O")
                        };
                        var uploadOptions = new BlobUploadOptions
                        {
                            HttpHeaders = blobHttpHeaders,
                            Metadata = metadata
                        };

                        await blobClient.UploadAsync(stream, uploadOptions);

                        var media = new Media
                        {
                            Id = Guid.NewGuid(),
                            FileName = Path.GetFileNameWithoutExtension(file.FileName),
                            FileType = fileExtension,
                            MimeType = file.ContentType,
                            Url = blobClient.Uri.ToString(),
                            FileSize = file.Length,
                            Type = mediaType,
                            Status = MediaStatus.Active,
                            EntityId = null,
                            EntityType = "Location",
                            CreatedAt = DateTime.UtcNow,
                            CreatedBy = userId,
                            Metadata = "{\"isTemporary\": false, \"expiresAt\": \"" + null + "\", \"originalFileName\": \"" + EscapeJsonString(file.FileName) + "\"}"
                        };

                        if (mediaType == MediaType.Image)
                        {
                            try
                            {
                                stream.Position = 0;
                                var imageData = await ProcessImageVariants(stream, blobName, folderPath);
                                media.Width = imageData.Width;
                                media.Height = imageData.Height;
                                media.ThumbnailUrl = imageData.ThumbnailUrl;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Failed to process image variants for temporary media {BlobName}", blobName);
                            }
                        }
                        await _mediaRepository.AddAsync(media);
                        await _mediaRepository.SaveChangesAsync();

                        results.Add(new MediaUploadResponse
                        {
                            Id = media.Id,
                            FileName = media.FileName,
                            FileType = media.FileType,
                            MimeType = media.MimeType,
                            Url = GenerateSecureUrl(media.Url),
                            ThumbnailUrl = !string.IsNullOrEmpty(media.ThumbnailUrl)
                                ? GenerateSecureUrl(media.ThumbnailUrl)
                                : null,
                            FileSize = media.FileSize,
                            Width = media.Width,
                            Height = media.Height,
                            CreatedAt = media.CreatedAt,
                            IsTemporary = true
                        });

                        _logger.LogInformation("Temporary media uploaded successfully: {MediaId}", media.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error uploading temporary file: {FileName}", SanitizeForLogging(file.FileName));
                        throw;
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in temporary media upload for user {UserId}", userId);
                throw;
            }
        }

        public async Task<List<MediaSummaryResponse>> GetUserTemporaryMediaAsync(Guid userId)
        {
            try
            {
                var query = await _mediaRepository.FindAsync(m =>
                    m.CreatedBy == userId &&
                    m.EntityId == null &&
                    m.Status == MediaStatus.Active &&
                    m.CreatedAt > DateTime.UtcNow.AddHours(-24)); // Only last 24 hours

                var mediaList = query.OrderByDescending(m => m.CreatedAt).ToList();

                var results = new List<MediaSummaryResponse>();
                foreach (var media in mediaList)
                {
                    results.Add(new MediaSummaryResponse
                    {
                        Id = media.Id,
                        Url = GenerateSecureUrl(media.Url),
                        ThumbnailUrl = !string.IsNullOrEmpty(media.ThumbnailUrl)
                            ? GenerateSecureUrl(media.ThumbnailUrl)
                            : null,
                        FileType = media.FileType,
                        Description = media.Description,
                        AltTag = media.AltTag,
                        Width = media.Width,
                        Height = media.Height
                    });
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting temporary media for user {UserId}", userId);
                throw;
            }
        }

        public async Task<bool> AttachMediaToPostAsync(List<Guid> mediaIds, Guid postId, Guid userId)
        {
            try
            {
                _logger.LogInformation("Attaching {Count} media items to post {PostId} by user {UserId}",
                    mediaIds.Count, postId, userId);

                var mediaItems = new List<Media>();

                // Validate all media items belong to user and are temporary
                foreach (var mediaId in mediaIds)
                {
                    var media = await _mediaRepository.GetByIdAsync(mediaId);
                    if (media == null)
                    {
                        throw new KeyNotFoundException($"Media with ID {mediaId} not found");
                    }

                    if (media.CreatedBy != userId)
                    {
                        throw new UnauthorizedAccessException($"Media {mediaId} does not belong to user {userId}");
                    }

                    if (media.EntityId != null)
                    {
                        throw new InvalidOperationException($"Media {mediaId} is already attached to an entity");
                    }

                    mediaItems.Add(media);
                }

                for (int i = 0; i < mediaItems.Count; i++)
                {
                    var media = mediaItems[i];

                    var oldBlobName = ExtractBlobNameFromUrl(media.Url);
                    var newFolderPath = $"posts/{postId}/{DateTime.UtcNow:yyyy/MM}";
                    var newBlobName = $"{newFolderPath}/{Path.GetFileName(oldBlobName)}";

                    var oldBlobClient = _containerClient.GetBlobClient(oldBlobName);
                    var newBlobClient = _containerClient.GetBlobClient(newBlobName);

                    await newBlobClient.StartCopyFromUriAsync(oldBlobClient.Uri);

                    var copyStatus = CopyStatus.Pending;
                    while (copyStatus == CopyStatus.Pending)
                    {
                        await Task.Delay(100);
                        var properties = await newBlobClient.GetPropertiesAsync();
                        copyStatus = properties.Value.BlobCopyStatus ?? CopyStatus.Failed;
                    }

                    if (copyStatus != CopyStatus.Success)
                    {
                        throw new InvalidOperationException($"Failed to copy media {media.Id} to permanent location");
                    }

                    media.Url = newBlobClient.Uri.ToString();
                    media.EntityId = postId;
                    media.EntityType = "Post";
                    media.SortOrder = i;
                    media.LastModifiedAt = DateTime.UtcNow;
                    media.LastModifiedBy = userId;
                    media.Metadata = null; 

                    if (!string.IsNullOrEmpty(media.ThumbnailUrl))
                    {
                        var oldThumbnailBlobName = ExtractBlobNameFromUrl(media.ThumbnailUrl);
                        var newThumbnailBlobName = $"{newFolderPath}/thumbnails/{Path.GetFileName(oldThumbnailBlobName)}";

                        var oldThumbnailClient = _containerClient.GetBlobClient(oldThumbnailBlobName);
                        var newThumbnailClient = _containerClient.GetBlobClient(newThumbnailBlobName);

                        await newThumbnailClient.StartCopyFromUriAsync(oldThumbnailClient.Uri);
                        media.ThumbnailUrl = newThumbnailClient.Uri.ToString();

                        await oldThumbnailClient.DeleteIfExistsAsync();
                    }

                    _mediaRepository.Update(media);

                    await oldBlobClient.DeleteIfExistsAsync();
                }

                await _mediaRepository.SaveChangesAsync();

                _logger.LogInformation("Successfully attached {Count} media items to post {PostId}",
                    mediaItems.Count, postId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error attaching media to post {PostId}", postId);
                throw;
            }
        }

        public async Task<ValidationResult> ValidateMediaFilesAsync(List<IFormFile> files)
        {
            try
            {
                const int maxFilesCount = 5;

                if (files == null || files.Count == 0)
                {
                    return new ValidationResult("No files provided");
                }

                if (files.Count > maxFilesCount)
                {
                    return new ValidationResult( $"Maximum {maxFilesCount} files allowed");
                }

                foreach (var file in files)
                {
                    if (file == null || file.Length == 0)
                    {
                        return new ValidationResult( "One or more files are empty");
                    }

                    if (file.Length > _settings.MaxFileSizeMB * 1024 * 1024)
                    {
                        return new ValidationResult(
                            $"File {file.FileName} exceeds maximum size of {_settings.MaxFileSizeMB} MB");
                    }

                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!_settings.AllowedFileTypes?.Contains(extension) == true)
                    {
                        return new ValidationResult(
                            $"File type {extension} is not supported for file {file.FileName}");
                    }
                }

                return new ValidationResult(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating media files");
                return new ValidationResult( "Validation error occurred");
            }
        }
        #region Helper Methods
        private string GenerateSecureUrl(string blobUrl)
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

                    var sasUri = blobClient.GenerateSasUri(sasBuilder);
                    _logger.LogDebug("Generated SAS URL for blob: {BlobName}", blobName);
                    return sasUri.ToString();
                }

                _logger.LogWarning("Cannot generate SAS URI for blob: {BlobName}", blobName);
                return blobUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure URL for blob: {BlobUrl}", blobUrl);
                return blobUrl;
            }
        }
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
            if (!_settings.AllowedFileTypes?.Contains(extension) == true)
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

        private string ExtractBlobNameFromUrl(string url)
        {
            try
            {
                var uri = new Uri(url);
                var segments = uri.Segments.Skip(2); 
                return string.Join("", segments);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting blob name from URL: {Url}", url);
                return string.Empty;
            }
        }

        private async Task<(int Width, int Height, string ThumbnailUrl)> ProcessImageVariants(Stream imageStream, string originalBlobName, string folderPath)
        {
            try
            {
                using var image = await Image.LoadAsync(imageStream);
                var originalWidth = image.Width;
                var originalHeight = image.Height;

                var thumbnailSize = 300;
                using var thumbnail = image.Clone(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(thumbnailSize, thumbnailSize),
                    Mode = ResizeMode.Crop,
                    Position = AnchorPositionMode.Center
                }));

                var originalFileName = Path.GetFileNameWithoutExtension(originalBlobName);
                var thumbnailBlobName = $"{folderPath}/thumbnails/{originalFileName}_thumb.jpg";

                using var thumbnailStream = new MemoryStream();
                await thumbnail.SaveAsJpegAsync(thumbnailStream, new JpegEncoder { Quality = 85 });
                thumbnailStream.Position = 0;

                var thumbnailBlobClient = _containerClient.GetBlobClient(thumbnailBlobName);

                var blobHttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "image/jpeg"
                };

                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = blobHttpHeaders,
                    Metadata = new Dictionary<string, string>
                    {
                        ["IsThumbnail"] = "true",
                        ["OriginalImage"] = originalBlobName,
                        ["CreatedAt"] = DateTime.UtcNow.ToString("O")
                    }
                };

                await thumbnailBlobClient.UploadAsync(thumbnailStream, uploadOptions);

                return (originalWidth, originalHeight, thumbnailBlobClient.Uri.ToString());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing image variants for {OriginalBlobName}", originalBlobName);
                throw;
            }
        }
        public async Task<string> GenerateSecureUrlAsync(string blobUrl)
        {
            try
            {
                if (string.IsNullOrEmpty(blobUrl))
                    return null;

                return  GenerateSecureUrl(blobUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating secure URL for blob: {BlobUrl}", blobUrl);
                return blobUrl;
            }
        }
        private static string SanitizeForAscii(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var bytes = Encoding.ASCII.GetBytes(input);
            var asciiString = Encoding.ASCII.GetString(bytes);

            return asciiString.Replace('?', '_');
        }

        private static string SanitizeForLogging(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return fileName;

            try
            {
                return SanitizeForAscii(fileName);
            }
            catch
            {
                return "[FILENAME_WITH_SPECIAL_CHARS]";
            }
        }

        private static string EscapeJsonString(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            return input.Replace("\\", "\\\\")
                        .Replace("\"", "\\\"")
                        .Replace("\n", "\\n")
                        .Replace("\r", "\\r")
                        .Replace("\t", "\\t");
        }
        #endregion
    }
}