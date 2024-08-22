using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging; // Add this using directive
using Newtonsoft.Json;
using web_crawling.Models;

public class ExtractorController : Controller
{
    private readonly IWebsiteContentExtractor _websiteContentExtractor;
    private readonly List<ProjectData> _projects;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<ExtractorController> _logger; // Add logger field

    public ExtractorController(IWebsiteContentExtractor websiteContentExtractor,
        IOptions<List<ProjectData>> projectDataOptions,
        IWebHostEnvironment webHostEnvironment,
        ILogger<ExtractorController> logger) // Inject logger
    {
        _websiteContentExtractor = websiteContentExtractor;
        _projects = projectDataOptions.Value;
        _webHostEnvironment = webHostEnvironment;
        _logger = logger; // Assign logger
    }

    [HttpGet]
    public IActionResult Index()
    {
        _logger.LogInformation("Accessing Index page with project list.");
        return View(_projects);
    }

    [HttpPost]
    public async Task<IActionResult> Submit(string selectedProjectName)
    {
        if (string.IsNullOrEmpty(selectedProjectName))
        {
            _logger.LogWarning("Project selection was empty.");
            ViewBag.Status = "Error";
            ViewBag.Message = "Project selection is required.";
            return View("Index", _projects);
        }

        var selectedProject = _projects.FirstOrDefault(p => p.Name == selectedProjectName);

        if (selectedProject == null)
        {
            _logger.LogWarning("Selected project '{ProjectName}' was not found.", selectedProjectName);
            ViewBag.Status = "Error";
            ViewBag.Message = "Selected project not found.";
            return View("Index", _projects);
        }

        if (string.IsNullOrEmpty(selectedProject.Url) || selectedProject.Data == null || !selectedProject.Data.Any())
        {
            _logger.LogWarning("Project '{ProjectName}' is missing URL or fields.", selectedProjectName);
            ViewBag.Status = "Error";
            ViewBag.Message = "Project URL or fields are missing.";
            return View("Index", _projects);
        }

        try
        {
            _logger.LogInformation("Starting data extraction for project '{ProjectName}'.", selectedProjectName);

            var allResults = new List<Dictionary<string, object>>();

            var result = await _websiteContentExtractor.ExtractDataFromUrlAsync(
                selectedProject.Name,
                selectedProject.Url,
                selectedProject.PageXpath,
                selectedProject.Data,
                selectedProject.LoginURL,
                selectedProject.LoginData,
                selectedProject.SubmitButtonXpath
            );

            allResults.Add(result);

            var jsonValue = JsonConvert.SerializeObject(allResults, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });

            ViewBag.TextValue = jsonValue;
            ViewBag.Status = "Success";
            ViewBag.Message = "Text extracted successfully.";
            ViewBag.Url = selectedProject.Url;
            ViewBag.Fields = selectedProject.Data;
            ViewBag.selectedProjectName = selectedProjectName;

            _logger.LogInformation("Data extraction succeeded for project '{ProjectName}'.", selectedProjectName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract data for project '{ProjectName}'.", selectedProjectName);
            ViewBag.Status = "Error";
            ViewBag.Message = $"Failed to extract data: {ex.Message}";
        }

        return View("Index", _projects);
    }
}
