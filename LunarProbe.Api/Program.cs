
using LunarProbe.Api.Data;
using LunarProbe.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<OllamaService>(client =>
{
    var baseUrl = builder.Configuration["Ollama:BaseUrl"]
        ?? "http://localhost:11434";

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int>(
            "Ollama:TimeoutSeconds", 180));
});

builder.Services.AddControllers();

builder.Services.AddScoped<ClaimExtractionService>();

builder.Services.AddDbContext<LpiDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("LpiDatabase")
        ?? "Data Source=data/lpi.db"));

var app = builder.Build();

var databasePath = Path.Combine(
    app.Environment.ContentRootPath,
    "data");

Directory.CreateDirectory(databasePath);

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider
        .GetRequiredService<LpiDbContext>();

    await DatabaseInitializer.InitializeAsync(dbContext);
}

app.MapControllers();

app.Run();

public partial class Program;
