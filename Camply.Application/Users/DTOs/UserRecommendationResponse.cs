using System;
using System.Collections.Generic;

namespace Camply.Application.Users.DTOs
{
    public class UserRecommendationResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Surname { get; set; }
        public string Username { get; set; }
        public string ProfileImageUrl { get; set; }
        public string Bio { get; set; }
        public int FollowersCount { get; set; }
        public int PostsCount { get; set; }
        public double Score { get; set; }
        public string RecommendationReason { get; set; }
        public List<string> MutualFollowers { get; set; } = new List<string>();
        public bool HasMutualFollowers { get; set; }
        public int MutualFollowersCount { get; set; }
        public DateTime LastActiveAt { get; set; }
        public bool IsVerified { get; set; }
    }

    public class UserRecommendationRequest
    {
        public Guid UserId { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string Algorithm { get; set; } = "smart"; // smart, popular, recent, mutual
        public bool IncludeMutualFollowers { get; set; } = true;
        public bool ExcludeAlreadyFollowed { get; set; } = true;
    }

    public class RecommendationSettings
    {
        public double MutualFollowersWeight { get; set; } = 0.4;
        public double PopularityWeight { get; set; } = 0.3;
        public double ActivityWeight { get; set; } = 0.2;
        public double ProfileCompletenessWeight { get; set; } = 0.1;
        public int MaxRecommendations { get; set; } = 50;
        public int MinFollowersForPopular { get; set; } = 10;
        public int MaxMutualFollowersToShow { get; set; } = 3;
    }
}