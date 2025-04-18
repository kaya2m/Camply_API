using Camply.API.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHttpClient();
builder.Services.AddDatabaseServices(builder.Configuration)
                .AddApplicationServices()
                .AddJwtAuthentication(builder.Configuration)
                .AddSwaggerConfiguration()
                .AddCorsConfiguration(builder.Configuration);

builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseApiConfiguration(app.Environment);

await app.UseDataInitializer();

app.Run();