using Hangfire;
using Hangfire.MySql;
using web_crawling.Models;

var builder = WebApplication.CreateBuilder(args);

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

//var webService = app.Services.GetRequiredService<IWebsiteContentExtractor>();

using (var scope = app.Services.CreateScope())
{
    var serviceProvider = scope.ServiceProvider;

    // Resolve the IUserService from the scoped service provider
    var webService = serviceProvider.GetRequiredService<IWebsiteContentExtractor>();

    // Call the static method and pass the IUserService

    RecurringJob.AddOrUpdate(
        "Scrape",
        () => JobScheduler.ExecuteJob(settings, webService),
        Cron.Minutely  // Adjust the schedule as needed
    );
}



app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Extractor}/{action=Index}/{id?}");

app.Run();