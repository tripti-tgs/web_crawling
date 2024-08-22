using HtmlAgilityPack;
using System.Xml.XPath;
using web_crawling.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;

public class WebsiteContentExtractor : IWebsiteContentExtractor, IDisposable
{
    // Selenium WebDriver for interacting with the browser
    private IWebDriver driver;

    // WebDriverWait to wait for certain conditions during interaction with the browser
    private WebDriverWait wait;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ILogger<WebsiteContentExtractor> _logger;

    public WebsiteContentExtractor(IWebHostEnvironment webHostEnvironment, ILogger<WebsiteContentExtractor> logger)
    {
        _webHostEnvironment = webHostEnvironment;
        _logger = logger;
    }

    private void InitializeWebDriver()
    {
        if (driver == null)
        {
            _logger.LogInformation("Initializing WebDriver.");
            driver = new ChromeDriver();
            wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
        }
    }

    public async Task<Dictionary<string, object>> ExtractDataFromUrlAsync(string? Name, string url,
        string? PageXpath, List<Field> fields, string? LoginURL,
        List<LoginField> LoginData, string? SubmitButtonXpath)
    {
        var results = new Dictionary<string, object> { { "Status", "Success" } };
        var allExtractedData = new List<Dictionary<string, object>>();

        try
        {
            _logger.LogInformation($"Starting data extraction from URL: {url}");

            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

                string html = await httpClient.GetStringAsync(url);
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                if (!string.IsNullOrWhiteSpace(LoginURL))
                {
                    InitializeWebDriver();
                    _logger.LogInformation($"Navigating to login URL: {LoginURL}");
                    driver.Navigate().GoToUrl(LoginURL);

                    foreach (var field in LoginData)
                    {
                        _logger.LogInformation($"Filling login field: {field.Name}");
                        driver.FindElement(By.Id(field.Name)).SendKeys(field.Value);
                    }

                    IWebElement loginButton = wait.Until(
                        ExpectedConditions.ElementToBeClickable(
                            By.XPath(SubmitButtonXpath)
                        )
                    );

                    _logger.LogInformation("Clicking the login button.");
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", loginButton);
                    loginButton.Click();
                }

                if (!string.IsNullOrWhiteSpace(PageXpath))
                {
                    InitializeWebDriver();
                    _logger.LogInformation($"Handling pagination with XPath: {PageXpath}");
                    driver.Navigate().GoToUrl(url);
                    bool hasNextPage = true;

                    while (hasNextPage)
                    {
                        var pageHtml = driver.PageSource;
                        htmlDoc.LoadHtml(pageHtml);
                        ExtractDataUsingXPaths(htmlDoc, fields, allExtractedData, results);

                        try
                        {
                            wait.Until(ExpectedConditions.ElementExists(By.XPath(PageXpath)));
                            var nextPageButton = wait.Until(ExpectedConditions.ElementExists(By.XPath(PageXpath)));
                            _logger.LogInformation("Clicking the next page button.");
                            nextPageButton.Click();
                            hasNextPage = true;
                        }
                        catch (WebDriverTimeoutException)
                        {
                            _logger.LogWarning($"Pagination XPath '{PageXpath}' did not match any elements. Pagination stopped.");
                            results["Status"] = "Warning";
                            results["PaginationError"] = $"Pagination XPath '{PageXpath}' did not match any elements. Pagination stopped.";
                            hasNextPage = false;
                        }
                        catch (NoSuchElementException)
                        {
                            _logger.LogWarning("No more pages available. Pagination stopped.");
                            hasNextPage = false;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"Error during pagination: {ex.Message}");
                            results["Status"] = "Error";
                            results["Error"] = $"Error during pagination: {ex.Message}";
                            hasNextPage = false;
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("No pagination detected, extracting data from the current page.");
                    ExtractDataUsingXPaths(htmlDoc, fields, allExtractedData, results);
                }
            }

            var allResults = new List<Dictionary<string, object>>();
            allResults.AddRange(allExtractedData);

            var jsonValue = JsonConvert.SerializeObject(allResults, Formatting.Indented, new JsonSerializerSettings
            {
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                NullValueHandling = NullValueHandling.Ignore
            });

            results["ExtractedData"] = jsonValue;
            await SaveJsonAsync(Name, jsonValue);
        }
        catch (Exception ex)
        {
            _logger.LogError($"An unexpected error occurred: {ex.Message}");
            results["Status"] = "Error";
            results["Error"] = $"An unexpected error occurred: {ex.Message}. Please try again later.";
        }
        finally
        {
            _logger.LogInformation("Disposing of WebDriver.");
            Dispose();
        }

        return results;
    }

    private void ExtractDataUsingXPaths(HtmlDocument htmlDoc, List<Field> fields, List<Dictionary<string, object>> allExtractedData, Dictionary<string, object> results)
    {
        var pageExtractedData = new List<Dictionary<string, object>>();

        foreach (var field in fields)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(field.Xpath))
                {
                    _logger.LogWarning($"XPath expression is missing or empty for field: {field.FieldName}");
                    results[field.FieldName] = "XPath expression is missing or empty.";
                    continue;
                }

                var nodes = htmlDoc.DocumentNode.SelectNodes(field.Xpath);
                if (nodes != null && nodes.Any())
                {
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        if (pageExtractedData.Count <= i)
                        {
                            pageExtractedData.Add(new Dictionary<string, object>());
                        }
                        pageExtractedData[i][field.FieldName] = ProcessNode(nodes[i]);
                    }
                }
                else
                {
                    _logger.LogWarning($"No elements found for the XPath: {field.Xpath}. Field: {field.FieldName}");
                    results[field.FieldName] = $"No elements found for the XPath: {field.Xpath}. Please verify the XPath expression.";
                }
            }
            catch (XPathException ex)
            {
                _logger.LogError($"Invalid XPath expression for field '{field.FieldName}': {ex.Message}");
                results[field.FieldName] = $"Invalid XPath expression for field '{field.FieldName}': {ex.Message}. Please check the XPath syntax.";
                results["Status"] = "Error";
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing field '{field.FieldName}': {ex.Message}");
                results[field.FieldName] = $"Error processing field '{field.FieldName}': {ex.Message}. Please check the XPath and ensure it is correct.";
                results["Status"] = "Error";
            }
        }

        allExtractedData.AddRange(pageExtractedData);
    }

    private async Task SaveJsonAsync(string selectedProjectName, string fileContent)
    {
        if (string.IsNullOrWhiteSpace(fileContent) || string.IsNullOrEmpty(selectedProjectName))
        {
            _logger.LogWarning("File content or project name is empty, skipping save operation.");
            return;
        }

        try
        {
            _logger.LogInformation($"Saving JSON content for project: {selectedProjectName}");

            var parsedJson = JToken.Parse(fileContent);
            string customDirectory = Path.Combine("Uploads", $"{selectedProjectName}-{DateTime.Now:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(customDirectory);

            string filePath = Path.Combine(customDirectory, $"{selectedProjectName}.json");

            string assetsDirectory = Path.Combine(customDirectory, "assets");
            Directory.CreateDirectory(assetsDirectory);

            await System.IO.File.WriteAllTextAsync(filePath, fileContent);

            var urls = ExtractUrlsFromJson(parsedJson);
            await DownloadFilesAsync(urls, assetsDirectory);

            _logger.LogInformation("JSON content and associated files saved successfully.");
        }
        catch (JsonException ex)
        {
            _logger.LogError($"Error parsing JSON: {ex.Message}");
        }
        catch (IOException ex)
        {
            _logger.LogError($"Error saving file: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Unexpected error: {ex.Message}");
        }
    }

    private IEnumerable<string> ExtractUrlsFromJson(JToken json)
    {
        var urls = new List<string>();
        TraverseJson(json, urls);
        return urls;
    }

    private void TraverseJson(JToken token, List<string> urls)
    {
        if (token.Type == JTokenType.String && Uri.IsWellFormedUriString(token.ToString(), UriKind.Absolute))
        {
            urls.Add(token.ToString());
        }

        if (token is JContainer container)
        {
            foreach (var child in container.Children())
            {
                TraverseJson(child, urls);
            }
        }
    }

    private async Task DownloadFilesAsync(IEnumerable<string> urls, string destinationFolder)
    {
        using (var httpClient = new HttpClient())
        {
            foreach (var url in urls)
            {
                try
                {
                    _logger.LogInformation($"Attempting to download file from URL: {url}");

                    var fileName = Path.GetFileName(new Uri(url).LocalPath);
                    var fileExtension = Path.GetExtension(fileName)?.ToLower();

                    // Determine the folder name based on file extension
                    string subFolder = fileExtension switch
                    {
                        ".pdf" => "pdf",
                        ".jpg" or ".jpeg" or ".png" or ".gif" => "images",
                        ".doc" or ".docx" => "documents",
                        ".zip" => "archives",
                        _ => "others" // Default folder for unsupported or unknown file types
                    };

                    // Combine the destination folder with the subfolder
                    var folderPath = Path.Combine(destinationFolder, subFolder);

                    // Ensure the subfolder exists
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                        _logger.LogInformation($"Created subfolder: {folderPath}");
                    }

                    var filePath = Path.Combine(folderPath, fileName);

                    var response = await httpClient.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    var fileContent = await response.Content.ReadAsByteArrayAsync();
                    await File.WriteAllBytesAsync(filePath, fileContent);

                    _logger.LogInformation($"Successfully downloaded and saved file: {filePath}");
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogError($"HTTP request error while downloading file from {url}: {ex.Message}");
                }
                catch (IOException ex)
                {
                    _logger.LogError($"I/O error while saving file from {url}: {ex.Message}");
                }
                catch (UriFormatException ex)
                {
                    _logger.LogError($"Invalid URL format for {url}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Unexpected error downloading file from {url}: {ex.Message}");
                }
            }
        }
    }

    private object ProcessNode(HtmlNode node)
    {
        _logger.LogInformation($"Processing node of type: {node.Name}");

        switch (node.Name.ToLower())
        {
            case "ul":
            case "ol":
                _logger.LogInformation("Extracting list items");
                var listItems = node.SelectNodes(".//li");
                if (listItems != null)
                {
                    var extractedItems = listItems.Select(li => li.InnerText.Trim()).ToList();
                    _logger.LogInformation($"Extracted {extractedItems.Count} list items");
                    return extractedItems;
                }
                else
                {
                    _logger.LogWarning("No list items found in the ul/ol element");
                    return new List<string>();
                }

            default:
                string text = node.InnerText.Trim();
                string imageUrl = node.GetAttributeValue("src", "");
                string linkUrl = node.GetAttributeValue("href", "");

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    _logger.LogInformation($"Extracted image URL: {imageUrl}");
                    return imageUrl;
                }
                if (!string.IsNullOrEmpty(linkUrl))
                {
                    _logger.LogInformation($"Extracted link URL: {linkUrl}");
                    return linkUrl;
                }

                _logger.LogInformation($"Extracted text content: {text}");
                return text;
        }
    }

    public void Dispose()
    {
        _logger.LogInformation("Disposing resources.");
        driver?.Quit();
        driver?.Dispose();
    }
}
