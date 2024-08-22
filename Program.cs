using Hangfire;
using Hangfire.MySql;
using Microsoft.Extensions.Logging;
using web_crawling.Models;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
.WriteTo.File("logs/myapp-.log",
    rollingInterval: RollingInterval.Day,
    rollOnFileSizeLimit: true,
    shared: true,
    flushToDiskInterval: TimeSpan.FromSeconds(1))
    .CreateLogger();

// Use Serilog for .NET Core logging
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Configure Hangfire
builder.Services.AddHangfire(configuration => configuration
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseStorage(new MySqlStorage(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        new MySqlStorageOptions()
    )));

// Add Hangfire server
builder.Services.AddHangfireServer();

// Configure strongly typed settings object
builder.Services.Configure<List<ProjectData>>(
    builder.Configuration.GetSection("WebsiteContentSettings"));

// Add the IWebsiteContentExtractor service
builder.Services.AddScoped<IWebsiteContentExtractor, WebsiteContentExtractor>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

// Enable Hangfire dashboard
app.UseHangfireDashboard();

var settings = app.Services.GetRequiredService<IConfiguration>()
             .GetSection("WebsiteContentSettings")
             .Get<List<WebsiteContentSetting>>();

using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;
    var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
    var webService = serviceProvider.GetRequiredService<IWebsiteContentExtractor>();

    logger.LogInformation("Scheduling job to scrape websites.");
    RecurringJob.AddOrUpdate(
        "Scrape",
        () => JobScheduler.ExecuteJob(settings, webService),
        Cron.Minutely  // Adjust the schedule as needed
    );
    logger.LogInformation("Job scheduled successfully.");
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Extractor}/{action=Index}/{id?}");

// Ensure any buffered logs are written before the app shuts down
app.Lifetime.ApplicationStopped.Register(Log.CloseAndFlush);

app.Run();