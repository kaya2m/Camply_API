using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Application.Common.Interfaces
{
    public interface ICacheService
    {
        // Basic operations
        Task<T> GetAsync<T>(string key);
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);
        Task<bool> RemoveAsync(string key);
        Task<long> RemovePatternAsync(string pattern);
        Task<bool> ExistsAsync(string key);

        // Hash operations (for complex objects)
        Task<T> HashGetAsync<T>(string key, string field);
        Task HashSetAsync<T>(string key, string field, T value, TimeSpan? expiration = null);
        Task<bool> HashDeleteAsync(string key, string field);
        Task<Dictionary<string, T>> HashGetAllAsync<T>(string key);

        // List operations (for feeds)
        Task<long> ListPushAsync<T>(string key, T value);
        Task<T> ListPopAsync<T>(string key);
        Task<List<T>> ListRangeAsync<T>(string key, long start = 0, long stop = -1);
        Task<long> ListLengthAsync(string key);

        // Set operations (for followers/following)
        Task<bool> SetAddAsync<T>(string key, T value);
        Task<bool> SetRemoveAsync<T>(string key, T value);
        Task<bool> SetContainsAsync<T>(string key, T value);
        Task<List<T>> SetMembersAsync<T>(string key);
        Task<long> SetLengthAsync(string key);

        // Counter operations (for likes, views)
        Task<long> IncrementAsync(string key, long value = 1);
        Task<long> DecrementAsync(string key, long value = 1);
        Task<long> GetCounterAsync(string key);

        // Utility operations
        Task<bool> SetExpirationAsync(string key, TimeSpan expiration);
        Task<TimeSpan?> GetTimeToLiveAsync(string key);
        Task FlushDatabaseAsync();
    }
}
