using web_crawling.Models;

public interface IWebsiteContentExtractor
{
    Task<Dictionary<string, object>> ExtractDataFromUrlAsync
        (
                string? Name,
        string url, 
        string PageXpath, 
        List<Field> fields,
        string? LoginURL,
        List<LoginField> LoginData,
        string ? SubmitButtonXpath

        );
}