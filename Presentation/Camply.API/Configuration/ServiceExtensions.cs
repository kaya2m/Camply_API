using Camply.Application.Auth.Interfaces;
using Camply.Application.Auth.Models;
using Camply.Application.Auth.Services;
using Camply.Domain.Common;
using Camply.Domain.Repositories;
using Camply.Infrastructure.Data;
using Camply.Infrastructure.Data.Repositories;
using Camply.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

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
            //services.AddScoped<IUserService, UserService>();

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
                x.RequireHttpsMetadata = false; // Production'da true yapılmalı
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

                // JWT Authentication için Swagger konfigürasyonu
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
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
    }
}
