namespace web_crawling.Models
{
    public class ProjectData
    {
        public string Name { get; set; }
        public string Url { get; set; }
        public List<Field> Data { get; set; }
        public int? Page { get; set; }
        public string DirectoryPath { get; set; }
        public string? FileName { get; set; }
    }
}
