using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Camply.Application.Common.Models;
using Camply.Application.Media.DTOs;
using Microsoft.AspNetCore.Http;

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
    }
}
