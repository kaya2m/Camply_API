using Camply.Application.Common.Models;
using Camply.Application.Media.DTOs;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Media.Interfaces
{
    public interface IMediaService
    {
        Task<MediaUploadResponse> UploadMediaAsync(Guid userId, IFormFile file);
        Task<MediaUploadResponse> UploadMediaFromStreamAsync(Guid userId, Stream fileStream, string fileName, string contentType);
        Task<MediaDetailResponse> GetMediaByIdAsync(Guid mediaId);
        Task<PagedResponse<MediaSummaryResponse>> GetMediaByUserAsync(Guid userId, int pageNumber, int pageSize);
        Task<PagedResponse<MediaSummaryResponse>> GetMediaByEntityAsync(Guid entityId, string entityType, int pageNumber, int pageSize);
        Task<bool> DeleteMediaAsync(Guid mediaId, Guid userId);
        Task<bool> UpdateMediaDetailsAsync(Guid mediaId, Guid userId, UpdateMediaRequest request);
        Task<bool> AssignMediaToEntityAsync(Guid mediaId, Guid entityId, string entityType, Guid userId);
        Task<bool> UnassignMediaFromEntityAsync(Guid mediaId, Guid userId);

        Task<List<MediaUploadResponse>> UploadPostMediaAsync(Guid userId, List<IFormFile> files);
        Task<List<MediaUploadResponse>> UploadLocationMediaAsync(Guid userId, List<IFormFile> files);
        Task<List<MediaSummaryResponse>> GetUserTemporaryMediaAsync(Guid userId);
        Task<bool> AttachMediaToPostAsync(List<Guid> mediaIds, Guid postId, Guid userId);
        Task<ValidationResult> ValidateMediaFilesAsync(List<IFormFile> files);

        Task<string> GenerateSecureUrlAsync(string blobUrl);
    }
}
