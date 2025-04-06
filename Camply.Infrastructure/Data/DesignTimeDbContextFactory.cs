using Microsoft.Extensions.Configuration;
using Camply.Domain.Common;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore;

namespace Camply.Infrastructure.Data
{
    public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CamplyDbContext>
    {
        public CamplyDbContext CreateDbContext(string[] args)
        {

            var baseDirectory = Directory.GetCurrentDirectory();
            var apiProjectPath = Path.Combine(baseDirectory, "..", "Presentation", "Camply.API");

            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(apiProjectPath)
                .AddJsonFile("appsettings.json")
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
                .Build();

            var builder = new DbContextOptionsBuilder<CamplyDbContext>();

            var connectionString = configuration.GetConnectionString("DefaultConnection");
            builder.UseNpgsql(connectionString,
                o => o.MigrationsAssembly("Camply.Infrastructure"));

            return new CamplyDbContext(
                builder.Options,
                new MockCurrentUserService(),
                new MockDateTimeService());
        }

        // Mock servisler
        private class MockCurrentUserService : ICurrentUserService
        {
            public Guid? UserId => null;
            public string UserName => "System";
            public bool IsAuthenticated => false;
            public bool IsInRole(string role) => false;
        }

        private class MockDateTimeService : IDateTime
        {
            public DateTime Now => DateTime.UtcNow;
            public DateTime UtcNow => DateTime.UtcNow;
        }
    }
}
