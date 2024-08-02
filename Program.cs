using web_crawling.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register the WebsiteContentSettings configuration section
builder.Services.Configure<List<ProjectData>>(builder.Configuration.GetSection("WebsiteContentSettings"));

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Extractor}/{action=Index}/{id?}");

app.Run();
