namespace web_crawling.Models
{
    public class WebsiteContentSetting
    {
        public string? Name { get; set; }
        public string? Url { get; set; }
        public string? FileName { get; set; }
        public string? PageXpath { get; set; }
        public List<Field> Data { get; set; }
        public string? LoginURL { get; set; }
        public string? SubmitButtonXpath { get; set; }
        public List<LoginField> LoginData { get; set; }
        public string IntervalMinutes { get; set; }
    }
}
