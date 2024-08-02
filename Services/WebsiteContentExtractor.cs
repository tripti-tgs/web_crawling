using HtmlAgilityPack;
using System.Xml.XPath;
using web_crawling.Models;

public class WebsiteContentExtractor : IWebsiteContentExtractor
{
    public async Task<Dictionary<string, object>> ExtractDataFromUrlAsync(string url, int? totalPages, List<Field> fields)
    {
        var results = new Dictionary<string, object> { { "Status", "Success" } };
        var allExtractedData = new List<Dictionary<string, object>>();

        try
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

                bool hasPagination = url.Contains("{1}") && totalPages.HasValue && totalPages.Value > 1;
                int pagesToProcess = hasPagination ? totalPages.Value : 1;

                for (int currentPage = 1; currentPage <= pagesToProcess; currentPage++)
                {
                    string pageUrl = hasPagination ? url.Replace("{1}", currentPage.ToString()) : url;
                    string html = await httpClient.GetStringAsync(pageUrl);

                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(html);

                    var pageExtractedData = new List<Dictionary<string, object>>();

                    foreach (var field in fields)
                    {
                        try
                        {
                            if (string.IsNullOrWhiteSpace(field.Xpath))
                            {
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
                                string pageInfo = hasPagination ? $" on page {currentPage}" : "";
                                results[field.FieldName] = $"No elements found for the XPath: {field.Xpath}{pageInfo}. Please verify the XPath expression.";
                            }
                        }
                        catch (XPathException ex)
                        {
                            string pageInfo = hasPagination ? $" on page {currentPage}" : "";
                            results[field.FieldName] = $"Invalid XPath expression for field '{field.FieldName}'{pageInfo}: {ex.Message}. Please check the XPath syntax.";
                            results["Status"] = "Error";
                        }
                        catch (Exception ex)
                        {
                            string pageInfo = hasPagination ? $" on page {currentPage}" : "";
                            results[field.FieldName] = $"Error processing field '{field.FieldName}'{pageInfo}: {ex.Message}. Please check the XPath and ensure it is correct.";
                            results["Status"] = "Error";
                        }
                    }

                    allExtractedData.AddRange(pageExtractedData);
                }
            }

            results["ExtractedData"] = allExtractedData;
        }
        catch (Exception ex)
        {
            results["Status"] = "Error";
            results["Error"] = $"An unexpected error occurred: {ex.Message}. Please try again later.";
        }

        return results;
    }


    private static object ProcessNode(HtmlNode node)
    {
        if (node.NodeType == HtmlNodeType.Element)
        {
            switch (node.Name.ToLower())
            {
                case "img":
                    var srcValue = node.GetAttributeValue("src", string.Empty);

                    // Check if src is empty and if so, use data-src
                    if (string.IsNullOrWhiteSpace(srcValue))
                    {
                        srcValue = node.GetAttributeValue("data-src", string.Empty);
                    }

                    // Create and return the dictionary with src and alt attributes
                    var imgAttributes = new Dictionary<string, string>
                    {
                        ["src"] = srcValue,
                        ["alt"] = node.GetAttributeValue("alt", "")
                    };

                    return imgAttributes;
                case "video":
                    return new Dictionary<string, string>
                    {
                        ["src"] = node.GetAttributeValue("src", ""),
                        ["poster"] = node.GetAttributeValue("poster", "")
                    };
                case "a":
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
                    return node.InnerText.Trim();
                case "p":
                    return node.InnerText.Trim();
                case "ul":
                case "ol":
                    // Process list items within <ul> or <ol>
                    var listItems = node.SelectNodes(".//li");
                    if (listItems != null && listItems.Any())
                    {
                        return listItems.Select(li => li.InnerText.Trim()).ToList();
                    }
                    return new List<string>();
                default:
                    // Handle other cases
                    return new List<string> { node.InnerHtml.Trim() };
            }
        }
        else if (node.NodeType == HtmlNodeType.Text)
        {
            return node.InnerText.Trim();
        }
        else
        {
            return node.InnerHtml.Trim();
        }
    }

}
