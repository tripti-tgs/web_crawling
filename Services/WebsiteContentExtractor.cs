using HtmlAgilityPack;
using System.Xml.XPath;
using web_crawling.Models;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;

public class WebsiteContentExtractor : IWebsiteContentExtractor, IDisposable
{
    // Selenium WebDriver for interacting with the browser
    private readonly IWebDriver driver;

    // WebDriverWait to wait for certain conditions during interaction with the browser
    private readonly WebDriverWait wait;

    public WebsiteContentExtractor()
    {
        // Initialize ChromeDriver
        driver = new ChromeDriver();

        // Set a default wait time of 10 seconds for WebDriverWait
        wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
    }

    public async Task<Dictionary<string, object>> ExtractDataFromUrlAsync(string url, string? PageXpath, List<Field> fields, string? LoginURL, List<LoginField> LoginData, string? SubmitButtonXpath)
    {
        // Dictionary to store the results of the extraction process
        var results = new Dictionary<string, object> { { "Status", "Success" } };

        // List to store all extracted data from pages
        var allExtractedData = new List<Dictionary<string, object>>();

        try
        {
            // Create an instance of HttpClient for making HTTP requests
            using (var httpClient = new HttpClient())
            {
                // Add a User-Agent header to mimic a request from a web browser
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

                // Fetch HTML content from the given URL
                string html = await httpClient.GetStringAsync(url);

                // Load the HTML content into HtmlAgilityPack for parsing
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(html);

                // If LoginURL is provided, perform login operations using Selenium
                if (!string.IsNullOrWhiteSpace(LoginURL))
                {
                    // Navigate to the login URL
                    driver.Navigate().GoToUrl(LoginURL);

                    // Fill out the login form with provided credentials
                    foreach (var field in LoginData)
                    {
                        driver.FindElement(By.Id(field.Name)).SendKeys(field.Value);
                    }

                    // Wait for the submit button to be clickable
                    IWebElement loginButton = wait.Until(
                        ExpectedConditions.ElementToBeClickable(
                            By.XPath(SubmitButtonXpath)
                        )
                    );

                    // Scroll to the submit button and click it
                    ((IJavaScriptExecutor)driver).ExecuteScript("arguments[0].scrollIntoView(true);", loginButton);
                    loginButton.Click();
                }

                // If PageXpath is provided, handle pagination using Selenium
                if (!string.IsNullOrWhiteSpace(PageXpath))
                {
                    // Navigate to the initial URL
                    driver.Navigate().GoToUrl(url);
                    bool hasNextPage = true;

                    while (hasNextPage)
                    {
                        // Extract data from the current page
                        var pageHtml = driver.PageSource;
                        htmlDoc.LoadHtml(pageHtml);
                        ExtractDataUsingXPaths(htmlDoc, fields, allExtractedData, results);

                        try
                        {
                            // Wait for the pagination element to be available
                            wait.Until(ExpectedConditions.ElementExists(By.XPath(PageXpath)));

                            // Click the pagination button
                            var nextPageButton = wait.Until(ExpectedConditions.ElementExists(By.XPath(PageXpath)));
                            nextPageButton.Click();
                            hasNextPage = true;
                        }
                        catch (WebDriverTimeoutException)
                        {
                            // If the pagination element is not found within the timeout period
                            hasNextPage = false;
                            results["Status"] = "Warning";
                            results["PaginationError"] = $"Pagination XPath '{PageXpath}' did not match any elements. Pagination stopped.";
                        }
                        catch (NoSuchElementException)
                        {
                            // If no more pages are available
                            hasNextPage = false;
                            Console.WriteLine("Element not found, retrying...");
                            System.Threading.Thread.Sleep(1000);
                        }
                        catch (Exception ex)
                        {
                            // Handle any other errors during pagination
                            results["Status"] = "Error";
                            results["Error"] = $"Error during pagination: {ex.Message}";
                            hasNextPage = false;
                        }
                    }
                }
                else
                {
                    // If no pagination, just extract data from the current page
                    ExtractDataUsingXPaths(htmlDoc, fields, allExtractedData, results);
                }
            }

            // Store the extracted data in the results dictionary
            results["ExtractedData"] = allExtractedData;
        }
        catch (Exception ex)
        {
            // Handle any unexpected errors during the extraction process
            results["Status"] = "Error";
            results["Error"] = $"An unexpected error occurred: {ex.Message}. Please try again later.";
        }
        finally
        {
            // Dispose of the Selenium WebDriver
            Dispose();
        }

        // Return the results of the extraction process
        return results;
    }

    // Method to extract data from the HTML document using provided XPaths
    private void ExtractDataUsingXPaths(HtmlDocument htmlDoc, List<Field> fields, List<Dictionary<string, object>> allExtractedData, Dictionary<string, object> results)
    {
        // List to store data extracted from the current page
        var pageExtractedData = new List<Dictionary<string, object>>();

        // Iterate over each field to extract data using its XPath
        foreach (var field in fields)
        {
            try
            {
                // Skip if XPath is missing or empty
                if (string.IsNullOrWhiteSpace(field.Xpath))
                {
                    results[field.FieldName] = "XPath expression is missing or empty.";
                    continue;
                }

                // Select nodes matching the provided XPath
                var nodes = htmlDoc.DocumentNode.SelectNodes(field.Xpath);
                if (nodes != null && nodes.Any())
                {
                    // Iterate over the matched nodes
                    for (int i = 0; i < nodes.Count; i++)
                    {
                        // Ensure there is a dictionary for each node in pageExtractedData
                        if (pageExtractedData.Count <= i)
                        {
                            pageExtractedData.Add(new Dictionary<string, object>());
                        }
                        // Process the node and add the result to the dictionary
                        pageExtractedData[i][field.FieldName] = ProcessNode(nodes[i]);
                    }
                }
                else
                {
                    // If no elements are found for the XPath, store a warning message
                    results[field.FieldName] = $"No elements found for the XPath: {field.Xpath}. Please verify the XPath expression.";
                }
            }
            catch (XPathException ex)
            {
                // Handle invalid XPath expressions
                results[field.FieldName] = $"Invalid XPath expression for field '{field.FieldName}': {ex.Message}. Please check the XPath syntax.";
                results["Status"] = "Error";
            }
            catch (Exception ex)
            {
                // Handle any other errors during the data extraction
                results[field.FieldName] = $"Error processing field '{field.FieldName}': {ex.Message}. Please check the XPath and ensure it is correct.";
                results["Status"] = "Error";
            }
        }

        // Add the extracted data from the current page to the overall list
        allExtractedData.AddRange(pageExtractedData);
    }

    // Method to process an HtmlNode and extract relevant data based on its type
    private static object ProcessNode(HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Element)
        {
            // Handle specific HTML elements
            switch (node.Name.ToLower())
            {
                case "img":
                    // Extract image source and alt text
                    var srcValue = node.GetAttributeValue("src", string.Empty);
                    if (string.IsNullOrWhiteSpace(srcValue))
                    {
                        srcValue = node.GetAttributeValue("data-src", string.Empty);
                    }
                    return new Dictionary<string, string>
                    {
                        ["src"] = srcValue,
                        ["alt"] = node.GetAttributeValue("alt", "")
                    };
                case "video":
                    // Extract video source and poster image
                    return new Dictionary<string, string>
                    {
                        ["src"] = node.GetAttributeValue("src", ""),
                        ["poster"] = node.GetAttributeValue("poster", "")
                    };
                case "a":
                    // Extract hyperlink href and text
                    return new Dictionary<string, string>
                    {
                        ["href"] = node.GetAttributeValue("href", ""),
                        ["text"] = node.InnerText.Trim()
                    };
                case "h1":
                case "h2":
                case "h3":
                case "h4":
                case "h5":
                case "h6":
                case "p":
                    // Extract text from headers and paragraphs
                    return node.InnerText.Trim();
                case "ul":
                case "ol":
                    // Extract list items
                    var listItems = node.SelectNodes(".//li");
                    return listItems != null ? listItems.Select(li => li.InnerText.Trim()).ToList() : new List<string>();
                default:
                    // For other elements, return inner HTML
                    return new List<string> { node.InnerHtml.Trim() };
            }
        }
        else if (node.NodeType == HtmlNodeType.Text)
        {
            // Extract text content
            return node.InnerText.Trim();
        }
        else
        {
            // Extract inner HTML for other node types
            return node.InnerHtml.Trim();
        }
    }

    // Dispose method to clean up the WebDriver instance
    public void Dispose()
    {
        driver?.Quit();
        driver?.Dispose();
    }
}
