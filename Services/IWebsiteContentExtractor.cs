using web_crawling.Models;

public interface IWebsiteContentExtractor
{
    Task<Dictionary<string, object>> ExtractDataFromUrlAsync(string url, int? page, List<Field> fields);
}