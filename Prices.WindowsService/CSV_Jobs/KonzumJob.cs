using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Prices.WindowsService.Database;
using Prices.WindowsService.Helpers;
using Prices.WindowsService.POCO;

namespace Prices.WindowsService.CSV_Jobs
{
    public class KonzumJob : Base<KonzumJob>
    {
        private readonly ILogger<KonzumJob> _logger;
        private readonly string _basePageUrl = "https://www.konzum.hr/cjenici?page=";
        private readonly string _baseDownloadUrl = "https://www.konzum.hr";

        public KonzumJob(ILogger<KonzumJob> Logger, BaseJobDependencies Dependencies) 
            : base(Logger, Dependencies)
        {
            _logger = Logger;
        }

        public override async Task Work()
        {
            try
            {
                RetailerDataPOCO retailerData = await GetRetailerBasicDataAsync(RetailersEnum.KONZUM);

                List<RetailerBusinessUnitPOCO> stores = await GetStoresAsync(retailerData.retailerId);

                if (stores.Any())
                {
                    bool finished = false;
                    int page = 1;
                    List<DownloadsDataPOCO> downloadsData = new List<DownloadsDataPOCO>();
                    List<ImportLog> importLogs = new List<ImportLog>();

                    while (!finished)
                    {
                        string _pageUrl = _basePageUrl + page;

                        HtmlDocument? doc = await GetWebDocAsync(_pageUrl);

                        HtmlNode downloadElement = doc.DocumentNode.Descendants("section")
                        .Where(node => node.GetAttributeValue("class", "").Contains("py-1")).FirstOrDefault();

                        string csvDate = downloadElement.Descendants("h4").Where(node => node.GetAttributeValue("class", "").Contains("f-weight-bold")).FirstOrDefault().InnerHtml;
                        string todaysDate = DateTime.Now.ToString("dd.MM.yyyy.");

                        IEnumerable<HtmlNode> downloadUrls = downloadElement.Descendants("a")
                        .Where(node => node.GetAttributeValue("href", "").Contains("/cjenici/download"));

                        if (!downloadUrls.Any() || csvDate != todaysDate)
                        {
                            finished = true;
                        }

                        foreach (HtmlNode url in downloadUrls)
                        {

                            downloadsData.Add(new DownloadsDataPOCO
                            {
                                innerHtml = url.InnerHtml,
                                hrefDownload = url.Attributes["href"].Value
                            });
                        }
                        page++;
                    }

                    foreach (RetailerBusinessUnitPOCO store in stores)
                    {
                        var downloadData = downloadsData.Where(x => x.innerHtml.Contains(store.filename, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();

                        if (downloadData != null)
                        {
                            await DownloadCSVAsync(_baseDownloadUrl + downloadData.hrefDownload, store.retailerID, store.unitID, retailerData.csvDirectory);

                            importLogs.Add(new ImportLog { retailerID = store.retailerID, unitID = store.unitID});
                        }
                    }

                    if (importLogs.Any())
                    {
                        await InsertImportLogs(importLogs);
                        SetSleepMinutes(1440);
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
