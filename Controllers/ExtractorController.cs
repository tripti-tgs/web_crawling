using Hangfire;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using web_crawling.Models;

public class ExtractorController : Controller
{
    private readonly IWebsiteContentExtractor _websiteContentExtractor;
    private readonly List<ProjectData> _projects;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ExtractorController(IWebsiteContentExtractor websiteContentExtractor,
        IOptions<List<ProjectData>> projectDataOptions, IWebHostEnvironment webHostEnvironment)
    {
        _websiteContentExtractor = websiteContentExtractor;
        _projects = projectDataOptions.Value;
        _webHostEnvironment = webHostEnvironment;
    }

    [HttpGet]
    public IActionResult Index()
    {

        return View(_projects);
    }

    [HttpPost]
    public async Task<IActionResult> Submit(string selectedProjectName)
    {
        if (string.IsNullOrEmpty(selectedProjectName))
        {
            ViewBag.Status = "Error";
            ViewBag.Message = "Project selection is required.";
            return View("Index", _projects);
        }

        var selectedProject = _projects.FirstOrDefault(p => p.Name == selectedProjectName);

        if (selectedProject == null)
        {
            ViewBag.Status = "Error";
            ViewBag.Message = "Selected project not found.";
            return View("Index", _projects);
        }

        if (string.IsNullOrEmpty(selectedProject.Url) || selectedProject.Data == null || !selectedProject.Data.Any())
        {
            ViewBag.Status = "Error";
            ViewBag.Message = "Project URL or fields are missing.";
            return View("Index", _projects);
        }

        try
        {
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
        }
        catch (Exception ex)
        {
            ViewBag.Status = "Error";
            ViewBag.Message = $"Failed to extract data: {ex.Message}";
        }

        return View("Index", _projects);
    }

  
}
