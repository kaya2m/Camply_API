using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Infrastructure.Options
{
    public class MLSettings
    {
        public FeatureCalculationSettings FeatureCalculation { get; set; } = new();
        public CacheSettings Cache { get; set; } = new();
        public ModelSettings Models { get; set; } = new();
        public ExperimentSettings Experiments { get; set; } = new();
        public AnalyticsSettings Analytics { get; set; } = new();
    }

    public class FeatureCalculationSettings
    {
        public int UserFeatureUpdateIntervalHours { get; set; } = 6;
        public int ContentFeatureUpdateIntervalHours { get; set; } = 2;
        public int BatchSize { get; set; } = 100;
        public int MaxConcurrentJobs { get; set; } = 5;
    }

    public class CacheSettings
    {
        public int FeedCacheExpirationMinutes { get; set; } = 15;
        public int UserInterestCacheExpirationHours { get; set; } = 24;
        public int ModelPredictionCacheMinutes { get; set; } = 60;
    }

    public class ModelSettings
    {
        public string DefaultEngagementModel { get; set; } = "engagement_predictor_v1";
        public string DefaultContentEmbeddingModel { get; set; } = "content_embedding_v1";
        public string DefaultUserEmbeddingModel { get; set; } = "user_embedding_v1";
        public string ModelStoragePath { get; set; } = "/app/models/";
    }

    public class ExperimentSettings
    {
        public int DefaultTrafficPercentage { get; set; } = 10;
        public int MinExperimentDurationDays { get; set; } = 7;
        public int MaxConcurrentExperiments { get; set; } = 5;
    }

    public class AnalyticsSettings
    {
        public int MetricsRetentionDays { get; set; } = 90;
        public int InteractionRetentionDays { get; set; } = 90;
        public int PredictionRetentionDays { get; set; } = 30;
    }
}
