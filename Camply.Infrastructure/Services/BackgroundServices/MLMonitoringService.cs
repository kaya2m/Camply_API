using Camply.Application.Common.Interfaces;
using Camply.Application.MachineLearning.Interfaces;
using Camply.Domain.Analytics;
using Camply.Domain.MachineLearning;
using Camply.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics.Metrics;
using System.Text.Json;

namespace Camply.Infrastructure.Services.MachineLearning
{
    public class ProductionMLMonitoringService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProductionMLMonitoringService> _logger;
        private readonly MLSettings _mlSettings;
        private readonly IEmailService _emailService;
        private readonly Meter _meter;
        private readonly Counter<long> _predictionCounter;
        private readonly Histogram<double> _predictionLatency;
        private readonly Gauge<double> _modelAccuracy;
        private readonly Counter<long> _errorCounter;

        public ProductionMLMonitoringService(
            IServiceProvider serviceProvider,
            ILogger<ProductionMLMonitoringService> logger,
            IOptions<MLSettings> mlSettings,
            IEmailService emailService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _mlSettings = mlSettings.Value;
            _emailService = emailService;

            // Initialize metrics
            _meter = new Meter("Camply.ML", "1.0.0");
            _predictionCounter = _meter.CreateCounter<long>("ml_predictions_total", "count", "Total number of ML predictions");
            _predictionLatency = _meter.CreateHistogram<double>("ml_prediction_latency", "milliseconds", "ML prediction latency");
            _modelAccuracy = _meter.CreateGauge<double>("ml_model_accuracy", "ratio", "Current model accuracy");
            _errorCounter = _meter.CreateCounter<long>("ml_errors_total", "count", "Total number of ML errors");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Production ML Monitoring Service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformHealthChecksAsync(stoppingToken);
                    await AnalyzeModelPerformanceAsync(stoppingToken);
                    await CheckDataDriftAsync(stoppingToken);
                    await MonitorResourceUsageAsync(stoppingToken);
                    await GeneratePerformanceReportAsync(stoppingToken);

                    // Run monitoring every 15 minutes
                    await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("ML Monitoring Service is stopping");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in ML Monitoring Service");
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                }
            }
        }

        private async Task PerformHealthChecksAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var modelRepository = scope.ServiceProvider.GetRequiredService<IMLModelRepository>();
            var analyticsRepository = scope.ServiceProvider.GetRequiredService<IMLAnalyticsRepository>();

            var healthChecks = new List<HealthCheckResult>();

            // Check active models
            var activeModels = await modelRepository.FindAsync(m => m.IsActive);
            foreach (var model in activeModels)
            {
                var health = await CheckModelHealthAsync(model);
                healthChecks.Add(health);

                if (health.Status == HealthStatus.Critical)
                {
                    await SendAlertAsync($"Critical: Model {model.Name} is unhealthy", health.Details);
                }
            }

            // Check prediction latency
            var latencyHealth = await CheckPredictionLatencyAsync(analyticsRepository);
            healthChecks.Add(latencyHealth);

            // Check error rates
            var errorHealth = await CheckErrorRatesAsync(analyticsRepository);
            healthChecks.Add(errorHealth);

            // Log overall health status
            var criticalIssues = healthChecks.Count(h => h.Status == HealthStatus.Critical);
            var warningIssues = healthChecks.Count(h => h.Status == HealthStatus.Warning);

            if (criticalIssues > 0)
            {
                _logger.LogError("ML System Health: {Critical} critical issues, {Warning} warnings",
                    criticalIssues, warningIssues);
            }
            else if (warningIssues > 0)
            {
                _logger.LogWarning("ML System Health: {Warning} warnings", warningIssues);
            }
            else
            {
                _logger.LogInformation("ML System Health: All systems operational");
            }
        }

        private async Task<HealthCheckResult> CheckModelHealthAsync(MLModel model)
        {
            try
            {
                // Check if model file exists and is accessible
                if (!string.IsNullOrEmpty(model.ModelPath))
                {
                    var fileExists = await CheckModelFileExistsAsync(model.ModelPath);
                    if (!fileExists)
                    {
                        return new HealthCheckResult
                        {
                            ComponentName = $"Model_{model.Name}",
                            Status = HealthStatus.Critical,
                            Details = "Model file not accessible",
                            CheckedAt = DateTime.UtcNow
                        };
                    }
                }

                // Check model age
                var daysSinceTraining = (DateTime.UtcNow - model.TrainedAt).TotalDays;
                if (daysSinceTraining > 14)
                {
                    return new HealthCheckResult
                    {
                        ComponentName = $"Model_{model.Name}",
                        Status = HealthStatus.Warning,
                        Details = $"Model is {daysSinceTraining:F1} days old, consider retraining",
                        CheckedAt = DateTime.UtcNow
                    };
                }

                return new HealthCheckResult
                {
                    ComponentName = $"Model_{model.Name}",
                    Status = HealthStatus.Healthy,
                    Details = "Model is operational",
                    CheckedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking health of model {ModelId}", model.Id);
                return new HealthCheckResult
                {
                    ComponentName = $"Model_{model.Name}",
                    Status = HealthStatus.Critical,
                    Details = $"Health check failed: {ex.Message}",
                    CheckedAt = DateTime.UtcNow
                };
            }
        }

        private async Task<HealthCheckResult> CheckPredictionLatencyAsync(IMLAnalyticsRepository analyticsRepository)
        {
            try
            {
                var metrics = await analyticsRepository.GetAlgorithmMetricsAsync("prediction_performance",
                    DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

                if (!metrics.Any())
                {
                    return new HealthCheckResult
                    {
                        ComponentName = "PredictionLatency",
                        Status = HealthStatus.Warning,
                        Details = "No recent prediction metrics available",
                        CheckedAt = DateTime.UtcNow
                    };
                }

                var avgLatency = metrics.Average(m => m.Value);
                var maxLatency = metrics.Max(m => m.Value);

                if (avgLatency > 1000) // > 1 second
                {
                    return new HealthCheckResult
                    {
                        ComponentName = "PredictionLatency",
                        Status = HealthStatus.Critical,
                        Details = $"High average latency: {avgLatency:F0}ms (max: {maxLatency:F0}ms)",
                        CheckedAt = DateTime.UtcNow
                    };
                }
                else if (avgLatency > 500) // > 500ms
                {
                    return new HealthCheckResult
                    {
                        ComponentName = "PredictionLatency",
                        Status = HealthStatus.Warning,
                        Details = $"Elevated latency: {avgLatency:F0}ms (max: {maxLatency:F0}ms)",
                        CheckedAt = DateTime.UtcNow
                    };
                }

                // Update metrics
                _predictionLatency.Record(avgLatency);

                return new HealthCheckResult
                {
                    ComponentName = "PredictionLatency",
                    Status = HealthStatus.Healthy,
                    Details = $"Average latency: {avgLatency:F0}ms",
                    CheckedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking prediction latency");
                return new HealthCheckResult
                {
                    ComponentName = "PredictionLatency",
                    Status = HealthStatus.Critical,
                    Details = $"Latency check failed: {ex.Message}",
                    CheckedAt = DateTime.UtcNow
                };
            }
        }

        private async Task<HealthCheckResult> CheckErrorRatesAsync(IMLAnalyticsRepository analyticsRepository)
        {
            try
            {
                var metrics = await analyticsRepository.GetAlgorithmMetricsAsync("prediction_performance",
                    DateTime.UtcNow.AddHours(-1), DateTime.UtcNow);

                if (!metrics.Any())
                {
                    return new HealthCheckResult
                    {
                        ComponentName = "ErrorRate",
                        Status = HealthStatus.Warning,
                        Details = "No recent error metrics available",
                        CheckedAt = DateTime.UtcNow
                    };
                }

                var totalRequests = metrics.Sum(m => m.SegmentedValues.GetValueOrDefault("total_requests", 0));
                var errorCount = metrics.Sum(m => m.SegmentedValues.GetValueOrDefault("errors", 0));
                var errorRate = totalRequests > 0 ? (errorCount / totalRequests) * 100 : 0;

                if (errorRate > 5) // > 5% error rate
                {

                    _errorCounter.Add((long)errorCount);
                    return new HealthCheckResult
                    {
                        ComponentName = "ErrorRate",
                        Status = HealthStatus.Critical,
                        Details = $"High error rate: {errorRate:F2}% ({errorCount}/{totalRequests})",
                        CheckedAt = DateTime.UtcNow
                    };
                }
                else if (errorRate > 1) // > 1% error rate
                {
                    return new HealthCheckResult
                    {
                        ComponentName = "ErrorRate",
                        Status = HealthStatus.Warning,
                        Details = $"Elevated error rate: {errorRate:F2}%",
                        CheckedAt = DateTime.UtcNow
                    };
                }

                return new HealthCheckResult
                {
                    ComponentName = "ErrorRate",
                    Status = HealthStatus.Healthy,
                    Details = $"Error rate: {errorRate:F2}%",
                    CheckedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking error rates");
                return new HealthCheckResult
                {
                    ComponentName = "ErrorRate",
                    Status = HealthStatus.Critical,
                    Details = $"Error rate check failed: {ex.Message}",
                    CheckedAt = DateTime.UtcNow
                };
            }
        }

        private async Task AnalyzeModelPerformanceAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var modelRepository = scope.ServiceProvider.GetRequiredService<IMLModelRepository>();
            var analyticsRepository = scope.ServiceProvider.GetRequiredService<IMLAnalyticsRepository>();

            var activeModels = await modelRepository.FindAsync(m => m.IsActive);

            foreach (var model in activeModels)
            {
                try
                {
                    await AnalyzeIndividualModelPerformanceAsync(model, analyticsRepository);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error analyzing performance for model {ModelId}", model.Id);
                }
            }
        }

        private async Task AnalyzeIndividualModelPerformanceAsync(MLModel model, IMLAnalyticsRepository analyticsRepository)
        {
            // Get recent predictions for this model
            var predictions = await analyticsRepository.GetModelPredictionsAsync(
                Guid.Empty, model.Name, DateTime.UtcNow.AddDays(-1));

            if (!predictions.Any())
            {
                _logger.LogWarning("No recent predictions found for model {ModelName}", model.Name);
                return;
            }

            // Calculate accuracy metrics (simplified - in production you'd have ground truth data)
            var avgPredictionScore = predictions.Average(p => p.PredictionScore);
            var predictionVariance = CalculateVariance(predictions.Select(p => (double)p.PredictionScore));

            // Update model accuracy gauge
            _modelAccuracy.Record(avgPredictionScore, new KeyValuePair<string, object>("model", model.Name));

            // Check for prediction drift
            var recentPredictions = predictions.Where(p => p.CreatedAt >= DateTime.UtcNow.AddHours(-1));
            var olderPredictions = predictions.Where(p => p.CreatedAt < DateTime.UtcNow.AddHours(-1));

            if (recentPredictions.Any() && olderPredictions.Any())
            {
                var recentAvg = recentPredictions.Average(p => p.PredictionScore);
                var olderAvg = olderPredictions.Average(p => p.PredictionScore);
                var drift = Math.Abs(recentAvg - olderAvg);

                if (drift > 0.2) // 20% drift threshold
                {
                    _logger.LogWarning("Significant prediction drift detected for model {ModelName}: {Drift:F3}",
                        model.Name, drift);

                    await SendAlertAsync($"Prediction Drift Alert: {model.Name}",
                        $"Detected {drift:F3} prediction drift in model {model.Name}");
                }
            }

            _logger.LogInformation("Model {ModelName} performance: Avg={AvgScore:F3}, Variance={Variance:F3}",
                model.Name, avgPredictionScore, predictionVariance);
        }

        private async Task CheckDataDriftAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var analyticsRepository = scope.ServiceProvider.GetRequiredService<IMLAnalyticsRepository>();

            try
            {
                // Analyze recent user interactions for data drift
                var recentInteractions = await GetRecentInteractionStatsAsync(analyticsRepository);
                var historicalInteractions = await GetHistoricalInteractionStatsAsync(analyticsRepository);

                var driftScore = CalculateDataDriftScore(recentInteractions, historicalInteractions);

                if (driftScore > 0.3) // 30% drift threshold
                {
                    _logger.LogWarning("Significant data drift detected: {DriftScore:F3}", driftScore);
                    await SendAlertAsync("Data Drift Alert",
                        $"Detected significant data drift with score {driftScore:F3}. Model retraining may be required.");
                }
                else if (driftScore > 0.15) // 15% drift threshold
                {
                    _logger.LogInformation("Moderate data drift detected: {DriftScore:F3}", driftScore);
                }

                // Save drift metrics
                var driftMetric = new AlgorithmMetricsDocument
                {
                    MetricType = "data_drift",
                    AlgorithmVersion = "production_v2",
                    Timestamp = DateTime.UtcNow,
                    Value = (float)driftScore,
                    SegmentedValues = new Dictionary<string, float>
                    {
                        ["drift_score"] = (float)driftScore,
                        ["recent_samples"] = recentInteractions.Count,
                        ["historical_samples"] = historicalInteractions.Count
                    }
                };

                await analyticsRepository.SaveAlgorithmMetricAsync(driftMetric);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking data drift");
            }
        }

        private async Task<Dictionary<string, double>> GetRecentInteractionStatsAsync(IMLAnalyticsRepository analyticsRepository)
        {
            // This would analyze recent user interactions and extract feature distributions
            // Simplified implementation
            return new Dictionary<string, double>
            {
                ["avg_session_length"] = 120.5,
                ["like_rate"] = 0.15,
                ["comment_rate"] = 0.05,
                ["share_rate"] = 0.02
            };
        }

        private async Task<Dictionary<string, double>> GetHistoricalInteractionStatsAsync(IMLAnalyticsRepository analyticsRepository)
        {
            // This would analyze historical interactions for comparison
            // Simplified implementation
            return new Dictionary<string, double>
            {
                ["avg_session_length"] = 115.2,
                ["like_rate"] = 0.14,
                ["comment_rate"] = 0.06,
                ["share_rate"] = 0.025
            };
        }

        private double CalculateDataDriftScore(Dictionary<string, double> recent, Dictionary<string, double> historical)
        {
            var driftScores = new List<double>();

            foreach (var key in recent.Keys.Intersect(historical.Keys))
            {
                var recentValue = recent[key];
                var historicalValue = historical[key];

                if (historicalValue != 0)
                {
                    var relativeDiff = Math.Abs(recentValue - historicalValue) / Math.Abs(historicalValue);
                    driftScores.Add(relativeDiff);
                }
            }

            return driftScores.Any() ? driftScores.Average() : 0.0;
        }

        private async Task MonitorResourceUsageAsync(CancellationToken cancellationToken)
        {
            try
            {
                // Monitor memory usage
                var memoryUsage = GC.GetTotalMemory(false) / (1024 * 1024); // MB

                // Monitor prediction cache hit rate
                using var scope = _serviceProvider.CreateScope();
                var cacheService = scope.ServiceProvider.GetRequiredService<ICacheService>();

                // This would calculate cache hit rate from cache statistics
                var cacheHitRate = 0.85; // Placeholder

                if (memoryUsage > 1000) // > 1GB
                {
                    _logger.LogWarning("High memory usage detected: {MemoryUsage}MB", memoryUsage);
                }

                if (cacheHitRate < 0.7) // < 70% hit rate
                {
                    _logger.LogWarning("Low cache hit rate: {CacheHitRate:F2}", cacheHitRate);
                }

                // Save resource metrics
                using var scope2 = _serviceProvider.CreateScope();
                var analyticsRepository = scope2.ServiceProvider.GetRequiredService<IMLAnalyticsRepository>();

                var resourceMetric = new AlgorithmMetricsDocument
                {
                    MetricType = "resource_usage",
                    AlgorithmVersion = "production_v2",
                    Timestamp = DateTime.UtcNow,
                    Value = (float)memoryUsage,
                    SegmentedValues = new Dictionary<string, float>
                    {
                        ["memory_mb"] = (float)memoryUsage,
                        ["cache_hit_rate"] = (float)cacheHitRate
                    }
                };

                await analyticsRepository.SaveAlgorithmMetricAsync(resourceMetric);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error monitoring resource usage");
            }
        }

        private async Task GeneratePerformanceReportAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var analyticsRepository = scope.ServiceProvider.GetRequiredService<IMLAnalyticsRepository>();

                var report = new PerformanceReport
                {
                    GeneratedAt = DateTime.UtcNow,
                    TimeWindow = "Last 24 hours"
                };

                // Get metrics for last 24 hours
                var metrics = await analyticsRepository.GetAlgorithmMetricsAsync("prediction_performance",
                    DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

                if (metrics.Any())
                {
                    report.TotalPredictions = metrics.Sum(m => m.SegmentedValues.GetValueOrDefault("total_requests", 0));
                    report.AverageLatency = metrics.Average(m => m.Value);
                    report.ErrorRate = CalculateErrorRate(metrics);
                }

                // Get drift metrics
                var driftMetrics = await analyticsRepository.GetAlgorithmMetricsAsync("data_drift",
                    DateTime.UtcNow.AddDays(-1), DateTime.UtcNow);

                if (driftMetrics.Any())
                {
                    report.DataDriftScore = driftMetrics.Average(m => m.Value);
                }

                _logger.LogInformation("Performance Report: {Report}", JsonSerializer.Serialize(report));

                // Send daily report if it's early morning
                if (DateTime.UtcNow.Hour == 6)
                {
                    await SendDailyReportAsync(report);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating performance report");
            }
        }

        private double CalculateErrorRate(List<AlgorithmMetricsDocument> metrics)
        {
            var totalRequests = metrics.Sum(m => m.SegmentedValues.GetValueOrDefault("total_requests", 0));
            var totalErrors = metrics.Sum(m => m.SegmentedValues.GetValueOrDefault("errors", 0));

            return totalRequests > 0 ? (totalErrors / totalRequests) * 100 : 0;
        }

        private double CalculateVariance(IEnumerable<double> values)
        {
            var mean = values.Average();
            var squareDiffs = values.Select(x => Math.Pow(x - mean, 2));
            return squareDiffs.Average();
        }

        private async Task<bool> CheckModelFileExistsAsync(string modelPath)
        {
            try
            {
                if (modelPath.StartsWith("http"))
                {
                    // Check blob storage
                    var uri = new Uri(modelPath);
                    var blobClient = new Azure.Storage.Blobs.BlobClient(uri);
                    var response = await blobClient.ExistsAsync();
                    return response.Value;
                }
                else
                {
                    // Check local file system
                    return File.Exists(modelPath);
                }
            }
            catch
            {
                return false;
            }
        }

        private async Task SendAlertAsync(string subject, string message)
        {
            try
            {
                var adminEmail = "admin@thecamply.com"; // Configure in settings
                await _emailService.SendEmailAsync(adminEmail, $"[ML Alert] {subject}",
                    $"<h3>ML System Alert</h3><p>{message}</p><p>Generated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>");

                _logger.LogWarning("ML Alert sent: {Subject}", subject);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send ML alert: {Subject}", subject);
            }
        }

        private async Task SendDailyReportAsync(PerformanceReport report)
        {
            try
            {
                var adminEmail = "admin@thecamply.com";
                var htmlReport = GenerateHTMLReport(report);

                await _emailService.SendEmailAsync(adminEmail,
                    $"[ML Daily Report] {report.GeneratedAt:yyyy-MM-dd}", htmlReport);

                _logger.LogInformation("Daily ML report sent");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send daily ML report");
            }
        }

        private string GenerateHTMLReport(PerformanceReport report)
        {
            return $@"
                <html>
                <body>
                    <h2>ML System Daily Performance Report</h2>
                    <p><strong>Generated:</strong> {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC</p>
                    <p><strong>Time Window:</strong> {report.TimeWindow}</p>
                    
                    <h3>Key Metrics</h3>
                    <ul>
                        <li><strong>Total Predictions:</strong> {report.TotalPredictions:N0}</li>
                        <li><strong>Average Latency:</strong> {report.AverageLatency:F1}ms</li>
                        <li><strong>Error Rate:</strong> {report.ErrorRate:F2}%</li>
                        <li><strong>Data Drift Score:</strong> {report.DataDriftScore:F3}</li>
                    </ul>
                    
                    <h3>Status</h3>
                    <p>System Status: <strong style='color: {(report.ErrorRate < 1 ? "green" : "red")}''>
                        {(report.ErrorRate < 1 ? "Healthy" : "Needs Attention")}
                    </strong></p>
                </body>
                </html>";
        }
    }

    // Supporting Classes
    public class HealthCheckResult
    {
        public string ComponentName { get; set; }
        public HealthStatus Status { get; set; }
        public string Details { get; set; }
        public DateTime CheckedAt { get; set; }
    }

    public enum HealthStatus
    {
        Healthy,
        Warning,
        Critical
    }

    public class PerformanceReport
    {
        public DateTime GeneratedAt { get; set; }
        public string TimeWindow { get; set; }
        public float TotalPredictions { get; set; }
        public double AverageLatency { get; set; }
        public double ErrorRate { get; set; }
        public double DataDriftScore { get; set; }
    }

    // Production Configuration Updates
    public class ProductionMLSettings : MLSettings
    {
        public MonitoringSettings Monitoring { get; set; } = new();
        public AlertingSettings Alerting { get; set; } = new();
        public PerformanceSettings Performance { get; set; } = new();
    }

    public class MonitoringSettings
    {
        public int HealthCheckIntervalMinutes { get; set; } = 15;
        public int PerformanceReportIntervalHours { get; set; } = 24;
        public double DataDriftThreshold { get; set; } = 0.3;
        public double ErrorRateThreshold { get; set; } = 5.0;
        public int LatencyThresholdMs { get; set; } = 1000;
    }

    public class AlertingSettings
    {
        public bool EmailAlertsEnabled { get; set; } = true;
        public string AdminEmail { get; set; } = "admin@thecamply.com";
        public bool SlackAlertsEnabled { get; set; } = false;
        public string SlackWebhookUrl { get; set; }
    }

    public class PerformanceSettings
    {
        public int ModelCacheSizeLimit { get; set; } = 5;
        public int PredictionCacheExpirationMinutes { get; set; } = 60;
        public int MaxConcurrentPredictions { get; set; } = 100;
        public bool CircuitBreakerEnabled { get; set; } = true;
        public int CircuitBreakerThreshold { get; set; } = 5;
    }
}
    