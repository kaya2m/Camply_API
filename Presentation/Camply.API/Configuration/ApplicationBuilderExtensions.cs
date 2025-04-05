using Camply.API.Middleware;
using Camply.Domain.Auth;
using Camply.Domain.Enums;
using Camply.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Camply.API.Configuration
{
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Application pipeline yapılandırması
        /// </summary>
        public static IApplicationBuilder UseApiConfiguration(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/error");
                app.UseHsts();
            }

            // Global exception middleware
            app.UseMiddleware<ErrorHandlingMiddleware>();

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseCors("AllowSpecificOrigins");

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            return app;
        }

        /// <summary>
        /// Database migration ve seed işlemleri
        /// </summary>
        public static async Task<IApplicationBuilder> UseDataInitializer(this IApplicationBuilder app)
        {
            using (var scope = app.ApplicationServices.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    var context = services.GetRequiredService<CamplyDbContext>();

                    await context.Database.MigrateAsync();

                    await SeedData.InitializeAsync(context, services);
                }
                catch (Exception ex)
                {
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "Veritabanı migrasyonu veya seed işlemi sırasında bir hata oluştu.");
                }
            }

            return app;
        }
    }

    /// <summary>
    /// Seed verisi oluşturma işlemleri
    /// </summary>
    public static class SeedData
    {
        public static async Task InitializeAsync(CamplyDbContext context, IServiceProvider serviceProvider)
        {
            // Rolleri kontrol et ve yoksa ekle
            if (!await context.Roles.AnyAsync())
            {
                context.Roles.AddRange(
                    new Role
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                        Name = "Admin",
                        Description = "Sistem yöneticisi",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Role
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                        Name = "Moderator",
                        Description = "İçerik denetleyicisi",
                        CreatedAt = DateTime.UtcNow
                    },
                    new Role
                    {
                        Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                        Name = "User",
                        Description = "Standart kullanıcı",
                        CreatedAt = DateTime.UtcNow
                    }
                );

                await context.SaveChangesAsync();
            }

            // Admin kullanıcısı ekle
            if (!await context.Users.AnyAsync(u => u.Email == "admin@thecamply.com"))
            {
                var adminUser = new User
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Username = "admin",
                    Email = "admin@thecamply.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!"),
                    Status = UserStatus.Active,
                    IsEmailVerified = true,
                    CreatedAt = DateTime.UtcNow
                };

                context.Users.Add(adminUser);

                // Admin rolü ata
                context.UserRoles.Add(new UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    CreatedAt = DateTime.UtcNow
                });

                await context.SaveChangesAsync();
            }
        }
    }
}
