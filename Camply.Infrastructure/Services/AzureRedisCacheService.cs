using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Camply.Application.Common.Interfaces;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Camply.Infrastructure.Services
{
    public class AzureRedisCacheService : ICacheService, IDisposable
    {
        private readonly IConnectionMultiplexer _connectionMultiplexer;
        private readonly IDatabase _database;
        private readonly AzureRedisSettings _settings;
        private readonly ILogger<AzureRedisCacheService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public AzureRedisCacheService(
            IConnectionMultiplexer connectionMultiplexer,
            IOptions<AzureRedisSettings> settings,
            ILogger<AzureRedisCacheService> logger)
        {
            _connectionMultiplexer = connectionMultiplexer ?? throw new ArgumentNullException(nameof(connectionMultiplexer));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _database = connectionMultiplexer.GetDatabase(_settings.Database);
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true
            };

            _logger.LogInformation("Azure Redis Cache Service initialized for database: {Database}", _settings.Database);
        }

        #region Basic Operations

        public async Task<T> GetAsync<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            }

            try
            {
                var cacheKey = GenerateKey(key);
                var value = await _database.StringGetAsync(cacheKey);

                if (!value.HasValue)
                {
                    _logger.LogDebug("Cache miss for key: {Key}", key);
                    return default(T);
                }

                _logger.LogDebug("Cache hit for key: {Key}", key);

                if (typeof(T) == typeof(string))
                    return (T)(object)value.ToString();

                if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }

                return JsonSerializer.Deserialize<T>(value, _jsonOptions);
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting cache key: {Key}", key);
                return default(T);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error for key: {Key}", key);
                return default(T);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting cache key: {Key}", key);
                return default(T);
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            }

            if (value == null)
            {
                _logger.LogWarning("Attempting to cache null value for key: {Key}", key);
                return;
            }

            try
            {
                var cacheKey = GenerateKey(key);
                var expiry = expiration ?? TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);

                string serializedValue;

                if (typeof(T) == typeof(string))
                {
                    serializedValue = value.ToString();
                }
                else if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
                {
                    serializedValue = value.ToString();
                }
                else
                {
                    serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                }

                var success = await _database.StringSetAsync(cacheKey, serializedValue, expiry);

                if (success)
                {
                    _logger.LogDebug("Cache set successfully for key: {Key}, expiry: {Expiry}", key, expiry);
                }
                else
                {
                    _logger.LogWarning("Failed to set cache for key: {Key}", key);
                }
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error setting cache key: {Key}", key);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization error for key: {Key}", key);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error setting cache key: {Key}", key);
                throw;
            }
        }

        public async Task<bool> RemoveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            }

            try
            {
                var cacheKey = GenerateKey(key);
                var result = await _database.KeyDeleteAsync(cacheKey);

                _logger.LogDebug("Cache removed for key: {Key}, success: {Success}", key, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error removing cache key: {Key}", key);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error removing cache key: {Key}", key);
                return false;
            }
        }

        public async Task<long> RemovePatternAsync(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
            {
                throw new ArgumentException("Pattern cannot be null or empty", nameof(pattern));
            }

            try
            {
                var patternKey = GenerateKey(pattern);
                long deletedCount = 0;

                await foreach (var key in ScanKeysAsync(patternKey))
                {
                    if (await _database.KeyDeleteAsync(key))
                    {
                        deletedCount++;
                    }
                }

                _logger.LogDebug("Cache pattern removed: {Pattern}, deleted: {Count}", pattern, deletedCount);
                return deletedCount;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error removing cache pattern: {Pattern}", pattern);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error removing cache pattern: {Pattern}", pattern);
                return 0;
            }
        }

        public async Task<bool> ExistsAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            }

            try
            {
                var cacheKey = GenerateKey(key);
                return await _database.KeyExistsAsync(cacheKey);
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error checking cache key existence: {Key}", key);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error checking cache key existence: {Key}", key);
                return false;
            }
        }

        #endregion

        #region Hash Operations

        public async Task<T> HashGetAsync<T>(string key, string field)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            if (string.IsNullOrWhiteSpace(field))
                throw new ArgumentException("Hash field cannot be null or empty", nameof(field));

            try
            {
                var cacheKey = GenerateKey(key);
                var value = await _database.HashGetAsync(cacheKey, field);

                if (!value.HasValue)
                {
                    _logger.LogDebug("Hash field miss for key: {Key}, field: {Field}", key, field);
                    return default(T);
                }

                _logger.LogDebug("Hash field hit for key: {Key}, field: {Field}", key, field);

                if (typeof(T) == typeof(string))
                    return (T)(object)value.ToString();

                if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
                    return (T)Convert.ChangeType(value, typeof(T));

                return JsonSerializer.Deserialize<T>(value, _jsonOptions);
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting hash field: {Key}:{Field}", key, field);
                return default(T);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON deserialization error for hash field: {Key}:{Field}", key, field);
                return default(T);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting hash field: {Key}:{Field}", key, field);
                return default(T);
            }
        }

        public async Task HashSetAsync<T>(string key, string field, T value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            if (string.IsNullOrWhiteSpace(field))
                throw new ArgumentException("Hash field cannot be null or empty", nameof(field));
            if (value == null)
            {
                _logger.LogWarning("Attempting to set null value for hash field: {Key}:{Field}", key, field);
                return;
            }

            try
            {
                var cacheKey = GenerateKey(key);

                string serializedValue;
                if (typeof(T) == typeof(string))
                {
                    serializedValue = value.ToString();
                }
                else if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
                {
                    serializedValue = value.ToString();
                }
                else
                {
                    serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                }

                var success = await _database.HashSetAsync(cacheKey, field, serializedValue);

                if (expiration.HasValue)
                {
                    await _database.KeyExpireAsync(cacheKey, expiration.Value);
                }

                _logger.LogDebug("Hash field set for key: {Key}, field: {Field}, success: {Success}", key, field, success);
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error setting hash field: {Key}:{Field}", key, field);
                throw;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization error for hash field: {Key}:{Field}", key, field);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error setting hash field: {Key}:{Field}", key, field);
                throw;
            }
        }

        public async Task<bool> HashDeleteAsync(string key, string field)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            if (string.IsNullOrWhiteSpace(field))
                throw new ArgumentException("Hash field cannot be null or empty", nameof(field));

            try
            {
                var cacheKey = GenerateKey(key);
                var result = await _database.HashDeleteAsync(cacheKey, field);

                _logger.LogDebug("Hash field deleted for key: {Key}, field: {Field}, success: {Success}", key, field, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error deleting hash field: {Key}:{Field}", key, field);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error deleting hash field: {Key}:{Field}", key, field);
                return false;
            }
        }

        public async Task<Dictionary<string, T>> HashGetAllAsync<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var hashFields = await _database.HashGetAllAsync(cacheKey);

                var result = new Dictionary<string, T>();

                foreach (var field in hashFields)
                {
                    try
                    {
                        T value;
                        if (typeof(T) == typeof(string))
                        {
                            value = (T)(object)field.Value.ToString();
                        }
                        else if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
                        {
                            value = (T)Convert.ChangeType(field.Value, typeof(T));
                        }
                        else
                        {
                            value = JsonSerializer.Deserialize<T>(field.Value, _jsonOptions);
                        }

                        result[field.Name] = value;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error deserializing hash field: {Key}:{Field}", key, field.Name);
                    }
                }

                _logger.LogDebug("Hash get all for key: {Key}, field count: {Count}", key, result.Count);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting all hash fields: {Key}", key);
                return new Dictionary<string, T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting all hash fields: {Key}", key);
                return new Dictionary<string, T>();
            }
        }

        #endregion

        #region List Operations

        public async Task<long> ListPushAsync<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var cacheKey = GenerateKey(key);
                var serializedValue = SerializeValue(value);

                var result = await _database.ListLeftPushAsync(cacheKey, serializedValue);

                _logger.LogDebug("List push for key: {Key}, new length: {Length}", key, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error pushing to list: {Key}", key);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error pushing to list: {Key}", key);
                return 0;
            }
        }

        public async Task<T> ListPopAsync<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var value = await _database.ListLeftPopAsync(cacheKey);

                if (!value.HasValue)
                {
                    _logger.LogDebug("List pop miss for key: {Key}", key);
                    return default(T);
                }

                _logger.LogDebug("List pop hit for key: {Key}", key);
                return DeserializeValue<T>(value);
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error popping from list: {Key}", key);
                return default(T);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error popping from list: {Key}", key);
                return default(T);
            }
        }

        public async Task<List<T>> ListRangeAsync<T>(string key, long start = 0, long stop = -1)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var values = await _database.ListRangeAsync(cacheKey, start, stop);

                var result = new List<T>();
                foreach (var value in values)
                {
                    try
                    {
                        var deserializedValue = DeserializeValue<T>(value);
                        result.Add(deserializedValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error deserializing list item for key: {Key}", key);
                    }
                }

                _logger.LogDebug("List range for key: {Key}, item count: {Count}", key, result.Count);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting list range: {Key}", key);
                return new List<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting list range: {Key}", key);
                return new List<T>();
            }
        }

        public async Task<long> ListLengthAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var result = await _database.ListLengthAsync(cacheKey);

                _logger.LogDebug("List length for key: {Key}, length: {Length}", key, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting list length: {Key}", key);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting list length: {Key}", key);
                return 0;
            }
        }

        #endregion

        #region Set Operations

        public async Task<bool> SetAddAsync<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var cacheKey = GenerateKey(key);
                var serializedValue = SerializeValue(value);

                var result = await _database.SetAddAsync(cacheKey, serializedValue);

                _logger.LogDebug("Set add for key: {Key}, success: {Success}", key, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error adding to set: {Key}", key);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error adding to set: {Key}", key);
                return false;
            }
        }

        public async Task<bool> SetRemoveAsync<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var cacheKey = GenerateKey(key);
                var serializedValue = SerializeValue(value);

                var result = await _database.SetRemoveAsync(cacheKey, serializedValue);

                _logger.LogDebug("Set remove for key: {Key}, success: {Success}", key, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error removing from set: {Key}", key);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error removing from set: {Key}", key);
                return false;
            }
        }

        public async Task<bool> SetContainsAsync<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            try
            {
                var cacheKey = GenerateKey(key);
                var serializedValue = SerializeValue(value);

                var result = await _database.SetContainsAsync(cacheKey, serializedValue);

                _logger.LogDebug("Set contains for key: {Key}, contains: {Contains}", key, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error checking set membership: {Key}", key);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error checking set membership: {Key}", key);
                return false;
            }
        }

        public async Task<List<T>> SetMembersAsync<T>(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var values = await _database.SetMembersAsync(cacheKey);

                var result = new List<T>();
                foreach (var value in values)
                {
                    try
                    {
                        var deserializedValue = DeserializeValue<T>(value);
                        result.Add(deserializedValue);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error deserializing set member for key: {Key}", key);
                    }
                }

                _logger.LogDebug("Set members for key: {Key}, member count: {Count}", key, result.Count);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting set members: {Key}", key);
                return new List<T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting set members: {Key}", key);
                return new List<T>();
            }
        }

        public async Task<long> SetLengthAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var result = await _database.SetLengthAsync(cacheKey);

                _logger.LogDebug("Set length for key: {Key}, length: {Length}", key, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting set length: {Key}", key);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting set length: {Key}", key);
                return 0;
            }
        }

        #endregion

        #region Counter Operations

        public async Task<long> IncrementAsync(string key, long value = 1)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var result = await _database.StringIncrementAsync(cacheKey, value);

                _logger.LogDebug("Counter increment for key: {Key}, value: {Value}, new total: {Total}", key, value, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error incrementing counter: {Key}", key);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error incrementing counter: {Key}", key);
                return 0;
            }
        }

        public async Task<long> DecrementAsync(string key, long value = 1)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var result = await _database.StringDecrementAsync(cacheKey, value);

                _logger.LogDebug("Counter decrement for key: {Key}, value: {Value}, new total: {Total}", key, value, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error decrementing counter: {Key}", key);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error decrementing counter: {Key}", key);
                return 0;
            }
        }

        public async Task<long> GetCounterAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var value = await _database.StringGetAsync(cacheKey);

                if (!value.HasValue)
                {
                    _logger.LogDebug("Counter miss for key: {Key}", key);
                    return 0;
                }

                var result = long.TryParse(value, out var counter) ? counter : 0;
                _logger.LogDebug("Counter get for key: {Key}, value: {Value}", key, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting counter: {Key}", key);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting counter: {Key}", key);
                return 0;
            }
        }

        #endregion

        #region Utility Operations

        public async Task<bool> SetExpirationAsync(string key, TimeSpan expiration)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var result = await _database.KeyExpireAsync(cacheKey, expiration);

                _logger.LogDebug("Set expiration for key: {Key}, expiration: {Expiration}, success: {Success}",
                    key, expiration, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error setting expiration: {Key}", key);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error setting expiration: {Key}", key);
                return false;
            }
        }

        public async Task<TimeSpan?> GetTimeToLiveAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var result = await _database.KeyTimeToLiveAsync(cacheKey);

                _logger.LogDebug("Get TTL for key: {Key}, TTL: {TTL}", key, result);
                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting TTL: {Key}", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting TTL: {Key}", key);
                return null;
            }
        }

        public async Task FlushDatabaseAsync()
        {
            try
            {
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                await server.FlushDatabaseAsync(_settings.Database);

                _logger.LogWarning("Azure Redis database {Database} flushed", _settings.Database);
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error flushing database");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error flushing database");
                throw;
            }
        }

        #endregion

        #region Health Check

        public async Task<bool> IsHealthyAsync()
        {
            try
            {
                var testKey = GenerateKey("health_check");
                var testValue = $"health_test_{DateTime.UtcNow:yyyyMMddHHmmss}";

                // Test write
                var setResult = await _database.StringSetAsync(testKey, testValue, TimeSpan.FromSeconds(10));
                if (!setResult)
                {
                    return false;
                }

                // Test read
                var getValue = await _database.StringGetAsync(testKey);
                if (!getValue.HasValue || getValue != testValue)
                {
                    return false;
                }

                // Test delete
                await _database.KeyDeleteAsync(testKey);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed");
                return false;
            }
        }

        #endregion

        #region Private Helper Methods

        private string GenerateKey(string key)
        {
            return $"{_settings.InstanceName}:{key}";
        }

        private string SerializeValue<T>(T value)
        {
            if (typeof(T) == typeof(string))
            {
                return value.ToString();
            }

            if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
            {
                return value.ToString();
            }

            return JsonSerializer.Serialize(value, _jsonOptions);
        }

        private T DeserializeValue<T>(RedisValue value)
        {
            if (!value.HasValue)
            {
                return default(T);
            }

            if (typeof(T) == typeof(string))
            {
                return (T)(object)value.ToString();
            }

            if (typeof(T).IsPrimitive || typeof(T) == typeof(decimal))
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }

            return JsonSerializer.Deserialize<T>(value, _jsonOptions);
        }

        private async IAsyncEnumerable<RedisKey> ScanKeysAsync(string pattern)
        {
            var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());

            await foreach (var key in server.KeysAsync(database: _settings.Database, pattern: pattern))
            {
                yield return key;
            }
        }

        #endregion

        #region Batch Operations

        /// <summary>
        /// Gets multiple cache values in a single operation
        /// </summary>
        public async Task<Dictionary<string, T>> GetMultipleAsync<T>(IEnumerable<string> keys)
        {
            if (keys == null || !keys.Any())
            {
                return new Dictionary<string, T>();
            }

            try
            {
                var cacheKeys = keys.Select(GenerateKey).ToArray();
                var redisKeys = cacheKeys.Select(k => (RedisKey)k).ToArray();

                var values = await _database.StringGetAsync(redisKeys);
                var result = new Dictionary<string, T>();

                for (int i = 0; i < keys.Count(); i++)
                {
                    var originalKey = keys.ElementAt(i);
                    var value = values[i];

                    if (value.HasValue)
                    {
                        try
                        {
                            result[originalKey] = DeserializeValue<T>(value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error deserializing value for key: {Key}", originalKey);
                        }
                    }
                }

                _logger.LogDebug("Batch get completed for {RequestedCount} keys, {FoundCount} found",
                    keys.Count(), result.Count);

                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error in batch get operation");
                return new Dictionary<string, T>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in batch get operation");
                return new Dictionary<string, T>();
            }
        }

        /// <summary>
        /// Sets multiple cache values in a single operation
        /// </summary>
        public async Task<bool> SetMultipleAsync<T>(Dictionary<string, T> keyValuePairs, TimeSpan? expiration = null)
        {
            if (keyValuePairs == null || !keyValuePairs.Any())
            {
                return true;
            }

            try
            {
                var expiry = expiration ?? TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);
                var keyValues = new List<KeyValuePair<RedisKey, RedisValue>>();

                foreach (var kvp in keyValuePairs)
                {
                    if (kvp.Value != null)
                    {
                        var cacheKey = GenerateKey(kvp.Key);
                        var serializedValue = SerializeValue(kvp.Value);
                        keyValues.Add(new KeyValuePair<RedisKey, RedisValue>(cacheKey, serializedValue));
                    }
                }

                if (!keyValues.Any())
                {
                    return true;
                }

                var success = await _database.StringSetAsync(keyValues.ToArray());

                // Set expiration for all keys if specified
                if (success && expiration.HasValue)
                {
                    var tasks = keyValues.Select(kv => _database.KeyExpireAsync(kv.Key, expiry));
                    await Task.WhenAll(tasks);
                }

                _logger.LogDebug("Batch set completed for {Count} keys, success: {Success}",
                    keyValues.Count, success);

                return success;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error in batch set operation");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in batch set operation");
                return false;
            }
        }

        /// <summary>
        /// Removes multiple cache keys in a single operation
        /// </summary>
        public async Task<long> RemoveMultipleAsync(IEnumerable<string> keys)
        {
            if (keys == null || !keys.Any())
            {
                return 0;
            }

            try
            {
                var cacheKeys = keys.Select(k => (RedisKey)GenerateKey(k)).ToArray();
                var deletedCount = await _database.KeyDeleteAsync(cacheKeys);

                _logger.LogDebug("Batch remove completed for {RequestedCount} keys, {DeletedCount} deleted",
                    keys.Count(), deletedCount);

                return deletedCount;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error in batch remove operation");
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in batch remove operation");
                return 0;
            }
        }

        #endregion

        #region Advanced Operations

        /// <summary>
        /// Gets or sets a cache value with a factory function
        /// </summary>
        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            try
            {
                // Try to get from cache first
                var cachedValue = await GetAsync<T>(key);
                if (cachedValue != null && !cachedValue.Equals(default(T)))
                {
                    _logger.LogDebug("Cache hit for get-or-set operation: {Key}", key);
                    return cachedValue;
                }

                // Cache miss, use factory to get value
                _logger.LogDebug("Cache miss for get-or-set operation, using factory: {Key}", key);
                var value = await factory();

                if (value != null && !value.Equals(default(T)))
                {
                    await SetAsync(key, value, expiration);
                }

                return value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in get-or-set operation for key: {Key}", key);
                throw;
            }
        }

        /// <summary>
        /// Sets a cache value only if the key doesn't already exist
        /// </summary>
        public async Task<bool> SetIfNotExistsAsync<T>(string key, T value, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));
            if (value == null)
                return false;

            try
            {
                var cacheKey = GenerateKey(key);
                var expiry = expiration ?? TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);
                var serializedValue = SerializeValue(value);

                var success = await _database.StringSetAsync(cacheKey, serializedValue, expiry, When.NotExists);

                _logger.LogDebug("Set-if-not-exists for key: {Key}, success: {Success}", key, success);
                return success;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error in set-if-not-exists operation: {Key}", key);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in set-if-not-exists operation: {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Refreshes the expiration time of a cache key
        /// </summary>
        public async Task<bool> RefreshAsync(string key, TimeSpan? expiration = null)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var expiry = expiration ?? TimeSpan.FromMinutes(_settings.DefaultExpirationMinutes);

                var exists = await _database.KeyExistsAsync(cacheKey);
                if (!exists)
                {
                    return false;
                }

                var success = await _database.KeyExpireAsync(cacheKey, expiry);

                _logger.LogDebug("Refresh expiration for key: {Key}, success: {Success}", key, success);
                return success;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error refreshing key: {Key}", key);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error refreshing key: {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Gets cache statistics and information
        /// </summary>
        public async Task<Dictionary<string, object>> GetCacheInfoAsync()
        {
            try
            {
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                var info = await server.InfoAsync();

                var result = new Dictionary<string, object>();

                foreach (var section in info)
                {
                    foreach (var item in section)
                    {
                        result[item.Key] = item.Value;
                    }
                }

                // Add connection info
                result["IsConnected"] = _connectionMultiplexer.IsConnected;
                result["Database"] = _settings.Database;
                result["InstanceName"] = _settings.InstanceName;
                result["EndPoints"] = _connectionMultiplexer.GetEndPoints().Select(ep => ep.ToString()).ToArray();

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache info");
                return new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["IsConnected"] = _connectionMultiplexer.IsConnected
                };
            }
        }

        /// <summary>
        /// Gets the size of a cache key in bytes (fallback implementation for older Redis versions)
        /// </summary>
        public async Task<long> GetKeySizeAsync(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Cache key cannot be null or empty", nameof(key));

            try
            {
                var cacheKey = GenerateKey(key);
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());

                try
                {
                    var memoryUsageMethod = typeof(IServer).GetMethod("MemoryUsageAsync");
                    if (memoryUsageMethod != null)
                    {
                        var task = (Task<long?>)memoryUsageMethod.Invoke(server, new object[] { cacheKey, _settings.Database });
                        var size = await task;
                        _logger.LogDebug("Key size for {Key}: {Size} bytes", key, size);
                        return size ?? 0;
                    }
                }
                catch
                {
                    // Fall through to alternative method
                }

                try
                {
                    var result = await server.ExecuteAsync("DEBUG", "OBJECT", cacheKey);
                    if (result.IsNull)
                    {
                        var debugInfo = result.ToString();
                        var match = System.Text.RegularExpressions.Regex.Match(debugInfo, @"serializedlength:(\d+)");
                        if (match.Success && long.TryParse(match.Groups[1].Value, out var size))
                        {
                            _logger.LogDebug("Key size for {Key}: {Size} bytes (via DEBUG OBJECT)", key, size);
                            return size;
                        }
                    }
                }
                catch
                {
                    // Fall through to final fallback
                }

                // Final fallback: Estimate based on string length
                var value = await _database.StringGetAsync(cacheKey);
                if (value.HasValue)
                {
                    var estimatedSize = System.Text.Encoding.UTF8.GetByteCount(value);
                    _logger.LogDebug("Key size for {Key}: ~{Size} bytes (estimated)", key, estimatedSize);
                    return estimatedSize;
                }

                return 0;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error getting key size: {Key}", key);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error getting key size: {Key}", key);
                return 0;
            }
        }

        #endregion

        #region Pub/Sub Operations (for real-time features)

        /// <summary>
        /// Publishes a message to a Redis channel
        /// </summary>
        public async Task<long> PublishAsync<T>(string channel, T message)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentException("Channel cannot be null or empty", nameof(channel));
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            try
            {
                var subscriber = _connectionMultiplexer.GetSubscriber();
                var serializedMessage = SerializeValue(message);

                var result = await subscriber.PublishAsync(channel, serializedMessage);

                _logger.LogDebug("Published message to channel: {Channel}, subscribers notified: {Count}",
                    channel, result);

                return result;
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error publishing to channel: {Channel}", channel);
                return 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error publishing to channel: {Channel}", channel);
                return 0;
            }
        }

        /// <summary>
        /// Subscribes to a Redis channel
        /// </summary>
        public async Task SubscribeAsync<T>(string channel, Action<string, T> handler)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentException("Channel cannot be null or empty", nameof(channel));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            try
            {
                var subscriber = _connectionMultiplexer.GetSubscriber();

                await subscriber.SubscribeAsync(channel, (ch, message) =>
                {
                    try
                    {
                        var deserializedMessage = DeserializeValue<T>(message);
                        handler(ch, deserializedMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error handling message from channel: {Channel}", channel);
                    }
                });

                _logger.LogInformation("Subscribed to channel: {Channel}", channel);
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error subscribing to channel: {Channel}", channel);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error subscribing to channel: {Channel}", channel);
                throw;
            }
        }

        /// <summary>
        /// Unsubscribes from a Redis channel
        /// </summary>
        public async Task UnsubscribeAsync(string channel)
        {
            if (string.IsNullOrWhiteSpace(channel))
                throw new ArgumentException("Channel cannot be null or empty", nameof(channel));

            try
            {
                var subscriber = _connectionMultiplexer.GetSubscriber();
                await subscriber.UnsubscribeAsync(channel);

                _logger.LogInformation("Unsubscribed from channel: {Channel}", channel);
            }
            catch (RedisException ex)
            {
                _logger.LogError(ex, "Redis error unsubscribing from channel: {Channel}", channel);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error unsubscribing from channel: {Channel}", channel);
                throw;
            }
        }

        #endregion

        #region Connection Management

        /// <summary>
        /// Gets connection status information
        /// </summary>
        public ConnectionStatus GetConnectionStatus()
        {
            try
            {
                var endPoints = _connectionMultiplexer.GetEndPoints();
                var status = new ConnectionStatus
                {
                    IsConnected = _connectionMultiplexer.IsConnected,
                    EndPoints = endPoints.Select(ep => ep.ToString()).ToList(),
                    Database = _settings.Database,
                    InstanceName = _settings.InstanceName,
                    Configuration = _connectionMultiplexer.Configuration
                };

                foreach (var endPoint in endPoints)
                {
                    try
                    {
                        var server = _connectionMultiplexer.GetServer(endPoint);
                        status.ServerInfo.Add(endPoint.ToString(), new ServerInfo
                        {
                            IsConnected = server.IsConnected,
                            ServerType = server.ServerType.ToString(),
                            Version = server.Version?.ToString(),
                            IsReplica = server.IsReplica
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error getting server info for endpoint: {EndPoint}", endPoint);
                    }
                }

                return status;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting connection status");
                return new ConnectionStatus
                {
                    IsConnected = false,
                    Error = ex.Message
                };
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            try
            {
                _connectionMultiplexer?.Dispose();
                _logger.LogInformation("Azure Redis Cache Service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Azure Redis Cache Service");
            }
        }

        #endregion
    }

    #region Supporting Classes

    public class ConnectionStatus
    {
        public bool IsConnected { get; set; }
        public List<string> EndPoints { get; set; } = new();
        public int Database { get; set; }
        public string InstanceName { get; set; }
        public string Configuration { get; set; }
        public string Error { get; set; }
        public Dictionary<string, ServerInfo> ServerInfo { get; set; } = new();
    }

    public class ServerInfo
    {
        public bool IsConnected { get; set; }
        public string ServerType { get; set; }
        public string Version { get; set; }
        public bool IsReplica { get; set; }
    }

    #endregion
}