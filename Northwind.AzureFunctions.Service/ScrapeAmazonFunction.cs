using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

namespace Northwind.AzureFunctions.Service;

public class ScrapeAmazonFunction
{
    private const string relativePath = "10-NET-Cross-Platform-Development-websites/dp/1801077363";

    private readonly IHttpClientFactory clientFactory;

    public ScrapeAmazonFunction(IHttpClientFactory clientFactory)
    {
        this.clientFactory = clientFactory;
    }

    [FunctionName(nameof(ScrapeAmazonFunction))]
    public async Task Run([TimerTrigger("0 0 * * * *")] TimerInfo timer, ILogger log)
    {
        log.LogInformation("C# Timer trigger function executed at {0}.", System.DateTime.UtcNow);
        log.LogInformation($"C# Timer trigger function next three occurrences at: {timer.FormatNextOccurrences(3, System.DateTime.UtcNow)}.");
        HttpClient client = clientFactory.CreateClient("Amazon");
        HttpResponseMessage response = await client.GetAsync(relativePath);
        log.LogInformation($"Request: GET {client.BaseAddress}{relativePath}");

        if (response.IsSuccessStatusCode)
        {
            log.LogInformation($"Successful HTTP request.");
            Stream stream = await response.Content.ReadAsStreamAsync();
            GZipStream gzipStream = new(stream, CompressionMode.Decompress);
            StreamReader reader = new(gzipStream);
            string page = reader.ReadToEnd();
            int posBsr = page.IndexOf("Best Sellers Rank");
            string bsrSection = page.Substring(posBsr, 45);
            int posHash = bsrSection.IndexOf("#") + 1;
            int posSpaceAterHash = bsrSection.IndexOf(" ", posHash);
            string bsr = bsrSection.Substring(posHash, posSpaceAterHash - posHash);
            bsr = bsr.Replace(",", null);
            if (int.TryParse(bsr, out int bestSellersRank))
            {
                log.LogInformation($"Best Sellers Rank #{bestSellersRank:N0}.");
            }
            else
            {
                log.LogError($"Failed to extract BSR number from: {bsrSection}.");
            }
        }
        else
        {
            log.LogError($"Bad HTTP request.");
        }
    }

}