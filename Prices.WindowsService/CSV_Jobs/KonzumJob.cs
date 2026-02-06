using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Prices.WindowsService.Database;
using Prices.WindowsService.Helpers;
using Prices.WindowsService.POCO;

namespace Prices.WindowsService.CSV_Jobs
{
    public class KonzumJob : Base<KonzumJob>
    {
        private static readonly string jobName = "KonzumJob";
        private readonly ILogger<KonzumJob> _logger;
        private readonly string _basePageUrl = "https://www.konzum.hr/cjenici?page=";
        private readonly string _baseDownloadUrl = "https://www.konzum.hr";

        public KonzumJob(ILogger<KonzumJob> Logger, IDbConnectionFactory DbConnFactory, RetailersHelper RetailersHelper) : base(Logger, DbConnFactory, RetailersHelper, jobName, 1440, 5)
        {
            _logger = Logger;
        }

        public override async Task Work()
        {
            try
            {
                var retailerData = await GetRetailerBasicDataAsync(RetailersEnum.KONZUM);

                var stores = await GetStoresAsync(retailerData.retailerId);

                if (stores.Any())
                {
                    bool finished = false;
                    int page = 1;
                    List<DownloadDataPOCO> downloadsData = new List<DownloadDataPOCO>();

                    while (!finished)
                    {
                        string _pageUrl = _basePageUrl + page;

                        HtmlDocument? doc = await GetWebDocAsync(_pageUrl);

                        var downloadElement = doc.DocumentNode.Descendants("section")
                        .Where(node => node.GetAttributeValue("class", "").Contains("py-1")).FirstOrDefault();

                        var downloadUrls = downloadElement.Descendants("a")
                        .Where(node => node.GetAttributeValue("href", "").Contains("/cjenici/download"));

                        if (!downloadUrls.Any())
                        {
                            finished = true;
                        }

                        foreach (var url in downloadUrls)
                        {

                            downloadsData.Add(new DownloadDataPOCO
                            {
                                innerHtml = url.InnerHtml,
                                hrefDownload = url.Attributes["href"].Value
                            });

                        }
                        page++;
                    }

                    foreach (var store in stores)
                    {
                        var downloadData = downloadsData.Where(x => x.innerHtml.Contains(store.filename, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                        if (downloadData != null)
                        {
                            await DownloadCSVAsync(_baseDownloadUrl + downloadData.hrefDownload, store.retailerID, store.unitID, retailerData.csvDirectory);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            
        }     
    }
}
