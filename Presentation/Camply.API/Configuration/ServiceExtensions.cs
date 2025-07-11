﻿using Camply.Application.Auth.Interfaces;
using Camply.Application.Auth.Models;
using Camply.Application.Auth.Services;
using Camply.Application.Common.Interfaces;
using Camply.Application.Locations.Interfaces;
using Camply.Application.Media.Interfaces;
using Camply.Application.Messages.Interfaces;
using Camply.Application.Messages.Interfaces.Services;
using Camply.Application.Messages.Services;
using Camply.Application.Posts.Interfaces;
using Camply.Application.Users.Interfaces;
using Camply.Domain.Common;
using Camply.Domain.Repositories;
using Camply.Infrastructure.Data;
using Camply.Infrastructure.Data.Repositories;
using Camply.Infrastructure.ExternalServices;
using Camply.Infrastructure.Options;
using Camply.Infrastructure.Repositories.Messages;
using Camply.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MongoDB.Driver;
using StackExchange.Redis;
using System.Text;
using System.Threading.RateLimiting;

namespace Camply.API.Configuration
{
    public static class ServiceExtensions
    {
        /// <summary>
        /// Entity Framework ve PostgreSQL yapılandırması
        /// </summary>
        public static IServiceCollection AddDatabaseServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<CamplyDbContext>(options =>
            {
                options.UseNpgsql(
                    configuration.GetConnectionString("DefaultConnection"),
                    b => b.MigrationsAssembly("Camply.Infrastructure"));
            });

            return services;
        }
        public static IServiceCollection AddAzureRedisCache(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<AzureRedisSettings>(configuration.GetSection("AzureRedis"));

            services.AddSingleton<IConnectionMultiplexer>(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<AzureRedisSettings>>().Value;
                var logger = provider.GetRequiredService<ILogger<IConnectionMultiplexer>>();

                try
                {
                    ConfigurationOptions configuration;

                    if (!string.IsNullOrEmpty(settings.ConnectionString))
                    {
                        configuration = ConfigurationOptions.Parse(settings.ConnectionString);
                    }
                    else
                    {
                        configuration = new ConfigurationOptions
                        {
                            EndPoints = { $"{settings.HostName}:{settings.Port}" },
                            Password = settings.Password,
                            Ssl = settings.UseSsl,
                            AllowAdmin = settings.AllowAdmin
                        };
                    }

                    configuration.AbortOnConnectFail = settings.AbortOnConnectFail;
                    configuration.ConnectTimeout = settings.ConnectTimeout;
                    configuration.SyncTimeout = settings.SyncTimeout;
                    configuration.ConnectRetry = settings.ConnectRetry;
                    configuration.ReconnectRetryPolicy = new ExponentialRetry(5000, 60000);
                    configuration.KeepAlive = 180;

                    logger.LogInformation("Connecting to Azure Redis Cache: {EndPoint}", configuration.EndPoints.FirstOrDefault());

                    var connectionMultiplexer = ConnectionMultiplexer.Connect(configuration);

                    connectionMultiplexer.ConnectionFailed += (sender, args) =>
                    {
                        logger.LogError("Azure Redis connection failed: {Exception}", args.Exception?.Message);
                    };

                    connectionMultiplexer.ConnectionRestored += (sender, args) =>
                    {
                        logger.LogInformation("Azure Redis connection restored");
                    };

                    connectionMultiplexer.InternalError += (sender, args) =>
                    {
                        logger.LogError("Azure Redis internal error: {Exception}", args.Exception?.Message);
                    };

                    logger.LogInformation("Successfully connected to Azure Redis Cache");
                    return connectionMultiplexer;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to connect to Azure Redis Cache");
                    throw;
                }
            });

            // Cache service
            services.AddSingleton<ICacheService, AzureRedisCacheService>();

            return services;
        }
        public static async Task InitializeMLServicesAsync(this IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();

            try
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<CamplyDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var mongoContext = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
                await mongoContext.InitializeAsync();

                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            }
            catch (Exception ex)
            {
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Error initializing ML services");
                throw;
            }
        }
        /// <summary>
        /// Repository ve servis bağımlılıklarının kaydı
        /// </summary>
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            // Common services
            services.AddScoped<IDateTime, DateTimeService>();
            services.AddScoped<ICurrentUserService, CurrentUserService>();

            // Repositories
            services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

            // Auth services
            services.AddScoped<TokenService>();
            services.AddScoped<IAuthService, AuthService>();
            

            // User services
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IUserRecommendationService, OptimizedUserRecommendationService>();

            // Chat Services 
            services.AddScoped<IConversationRepository, ConversationRepository>();
            services.AddScoped<IMessageRepository, MessageRepository>();
            services.AddScoped<IReactionRepository, ReactionRepository>();
        
            services.AddScoped<IConversationService, ConversationService>();
            services.AddScoped<IMessageService, MessageService>();
            services.AddScoped<IReactionService, ReactionService>();

            services.AddScoped<ILocationService, LocationService>();
            services.AddScoped<ILocationReviewService, LocationReviewService>();
            services.AddScoped<ILocationCacheService, LocationCacheService>();
            services.AddScoped<ILocationAnalyticsService, LocationAnalyticsService>();
            //Post Services
            services.AddScoped<IPostService, EnhancedPostService>();

            return services;
        }
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Auth services
            services.AddScoped<GoogleAuthService>();
            services.AddScoped<FacebookAuthService>();

            //Email
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            services.AddScoped<IEmailService, EmailService>();

            //URL settings
            services.Configure<CodeSettings>(configuration.GetSection("CodeSettings"));
            services.AddScoped<ICodeBuilderService, CodeBuilderService>();

            // MongoDB yapılandırması
            services.Configure<MongoDbSettings>(configuration.GetSection("MongoDbSettings"));
            services.AddSingleton<MongoDbContext>();


           //Chat User Service
              services.AddSingleton<UserPresenceTracker>();
              
              // Background Services
              services.AddHostedService<RecommendationBackgroundService>();
              
            // Feed Cache Warmup Service
            services.AddHostedService<FeedCacheWarmupService>();

            return services;
        }
        /// <summary>
        /// Media hizmetlerinin kaydı ve yapılandırması
        /// </summary>
        public static IServiceCollection AddMediaServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<BlobStorageSettings>(configuration.GetSection("BlobStorage"));

            services.Configure<CloudflareSettings>(configuration.GetSection("Cloudflare"));

            services.AddHttpClient<CloudflareService>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("User-Agent", "Camply-API/1.0");
            });

            // Services
            services.AddScoped<IMediaService, BlobStorageMediaService>();
            services.AddScoped<ICloudflareService, CloudflareService>();
            services.AddScoped<IBlobStorageInitializer, BlobStorageInitializer>();

            return services;
        }

        /// <summary>
        /// JWT Authentication yapılandırması
        /// </summary>
        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            // JWT Settings
            var jwtSettings = configuration.GetSection("JwtSettings");
            services.Configure<JwtSettings>(jwtSettings);

            var key = Encoding.ASCII.GetBytes(jwtSettings["Secret"]);
            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(x =>
            {
                x.RequireHttpsMetadata = false;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSettings["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

            // Social Login settings
            services.Configure<SocialLoginSettings>(
                configuration.GetSection("SocialLoginSettings"));

            return services;
        }
        public static IServiceCollection AddSessionConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            var azureRedisConnectionString = configuration.GetSection("AzureRedis")["ConnectionString"];

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = azureRedisConnectionString;
                options.InstanceName = "CamplySession";
            });

            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.IsEssential = true;
                options.Cookie.Name = ".Camply.Session";
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax; 
            });

            return services;
        }
        /// <summary>
        /// Swagger API dokümantasyon yapılandırması
        /// </summary>
        public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
        {
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "TheCamply API",
                    Version = "v1",
                    Description = "TheCamply - Kampçılar İçin Sosyal Medya API'si",
                    Contact = new OpenApiContact
                    {
                        Name = "TheCamply Team",
                        Email = "contact@thecamply.com",
                        Url = new Uri("https://thecamply.com")
                    }
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            return services;
        }

        /// <summary>
        /// CORS yapılandırması
        /// </summary>
        public static IServiceCollection AddCorsConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowSpecificOrigins", policy =>
                {
                    policy.WithOrigins(
                            "https://thecamply.com",
                            "https://app.thecamply.com",
                            "http://localhost:3000",
                            "http://localhost:8080")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });

            return services;
        }

        public static IServiceCollection AddRateLimit(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("fixed", options =>
                {
                    options.PermitLimit = 20;
                    options.Window = TimeSpan.FromSeconds(30);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 0;
                });

                options.AddFixedWindowLimiter("forgot-password", options =>
                {
                    options.PermitLimit = 5; 
                    options.Window = TimeSpan.FromMinutes(5);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 0;
                });

                options.AddTokenBucketLimiter("ip", options =>
                {
                    options.TokenLimit = 20;
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 0;           
                    options.ReplenishmentPeriod = TimeSpan.FromSeconds(10); 
                    options.TokensPerPeriod = 4;      
                    options.AutoReplenishment = true; 
                });

                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";

                    var errorResponse = new
                    {
                        error = "Rate limit exceeded. Too many requests.",
                    };

                    await context.HttpContext.Response.WriteAsJsonAsync(errorResponse, token);
                };
                options.AddFixedWindowLimiter("ml-api", options =>
                {
                    options.PermitLimit = 50;
                    options.Window = TimeSpan.FromMinutes(1);
                    options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                    options.QueueLimit = 10;
                });
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.OnRejected = async (context, token) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    context.HttpContext.Response.ContentType = "application/json";

                    var errorResponse = new
                    {
                        error = "Rate limit exceeded. Too many requests.",
                        retryAfter = "Please wait before making another request."
                    };

                    await context.HttpContext.Response.WriteAsJsonAsync(errorResponse, token);
                };
            });
          
            return services;
        }
    }
}
