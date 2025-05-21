using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Services
{
    public class UserPresenceTracker
    {
        private static readonly ConcurrentDictionary<string, bool> OnlineUsers = new ConcurrentDictionary<string, bool>();
        private readonly IMemoryCache _cache;

        public UserPresenceTracker(IMemoryCache cache)
        {
            _cache = cache;
        }

        public async Task<bool> UserConnected(string userId)
        {
            OnlineUsers[userId] = true;
            _cache.Set($"user_last_seen_{userId}", DateTime.UtcNow, TimeSpan.FromDays(1));

            return await Task.FromResult(true);
        }

        public async Task<bool> UserDisconnected(string userId)
        {
            OnlineUsers.TryRemove(userId, out _);
            _cache.Set($"user_last_seen_{userId}", DateTime.UtcNow, TimeSpan.FromDays(1));

            return await Task.FromResult(true);
        }

        public async Task<bool> IsUserOnline(string userId)
        {
            return await Task.FromResult(OnlineUsers.ContainsKey(userId) && OnlineUsers[userId]);
        }

        public async Task<DateTime?> GetLastSeenAt(string userId)
        {
            if (!_cache.TryGetValue($"user_last_seen_{userId}", out DateTime lastSeen))
            {
                return null;
            }

            return await Task.FromResult(lastSeen);
        }

        public async Task<IEnumerable<string>> GetOnlineUsers()
        {
            return await Task.FromResult(OnlineUsers.Keys);
        }
    }
}