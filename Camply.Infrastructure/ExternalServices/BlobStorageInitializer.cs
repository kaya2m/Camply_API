using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Camply.Application.Common.Interfaces;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
namespace Camply.Infrastructure.ExternalServices
{
    public class BlobStorageInitializer : IBlobStorageInitializer
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly BlobStorageSettings _settings;
        private readonly ILogger<BlobStorageInitializer> _logger;

        public BlobStorageInitializer(
            IOptions<BlobStorageSettings> settings,
            ILogger<BlobStorageInitializer> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _blobServiceClient = new BlobServiceClient(_settings.ConnectionString);
        }

        public async Task InitializeAsync()
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(_settings.ContainerName);

                await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
                await SetCorsPolicy();
                _logger.LogInformation($"Blob storage container '{_settings.ContainerName}' initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize blob storage");
                throw;
            }
        }

        private async Task SetCorsPolicy()
        {
            try
            {
                var properties = await _blobServiceClient.GetPropertiesAsync();
                var cors = properties.Value.Cors.ToList();
                if (!cors.Any(c => c.AllowedOrigins.Contains("*")))
                {
                    cors.Add(new BlobCorsRule
                    {
                        AllowedOrigins = "*",
                        AllowedMethods = "GET,PUT,POST,DELETE,HEAD,OPTIONS",
                        AllowedHeaders = "*",
                        ExposedHeaders = "*",
                        MaxAgeInSeconds = 86400
                    });

                    var serviceProperties = new BlobServiceProperties
                    {
                        Cors = cors
                    };

                    await _blobServiceClient.SetPropertiesAsync(serviceProperties);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set CORS policy for blob storage");
            }
        }
    }

}
