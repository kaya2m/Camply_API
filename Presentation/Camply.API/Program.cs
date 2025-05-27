using Camply.API.Configuration;
using Camply.API.Hubs;
using Camply.Application.Common.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddDatabaseServices(builder.Configuration)
                .AddApplicationServices()
                .AddInfrastructureServices(builder.Configuration)
                  .AddMediaServices(builder.Configuration)
                .AddJwtAuthentication(builder.Configuration)
                .AddSwaggerConfiguration()
                .AddRateLimit(builder.Configuration)
                .AddCorsConfiguration(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddSignalR();
builder.Services.AddMemoryCache();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TheCamply API v1"));
}
else
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "TheCamply API v1"));
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseCors("AllowSpecificOrigins");
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers(); 
});

app.MapHub<ChatHub>("/chatHub");
await app.UseDataInitializer();

// Initialize blob storage
using (var scope = app.Services.CreateScope())
{
    var blobInitializer = scope.ServiceProvider.GetService<IBlobStorageInitializer>();
    if (blobInitializer != null)
    {
        await blobInitializer.InitializeAsync();
    }
}


app.Run();