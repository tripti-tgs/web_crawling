using web_crawling.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Hangfire;

public static class JobScheduler
{
   
    public static async Task ExecuteJob(List<WebsiteContentSetting> settings, 
        IWebsiteContentExtractor webService)
    {
      
            foreach (var setting in settings)
            {

            // Schedule the job
            RecurringJob.AddOrUpdate(
                $"Scrape-{setting.Name}",
                () =>
                 (webService.ExtractDataFromUrlAsync(
                    setting.Name,
                    setting.Url,
                    setting.PageXpath,
                    setting.Data,
                    setting.LoginURL,
                    setting.LoginData,
                    setting.SubmitButtonXpath
                )),
             setting.IntervalMinutes
        );
          

            }
        
    }
}
