using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Prices.WindowsService.Database;
using Prices.WindowsService.Helpers;
using System.Text.Json;
using Prices.WindowsService.POCO;

namespace Prices.WindowsService.CSV_Jobs
{
    public class KauflandJob : Base<KauflandJob>
    {
        private static readonly string jobName= "KauflandJob";
        private readonly ILogger<KauflandJob> _logger;
        private readonly string _basePageUrl = "https://www.kaufland.hr/akcije-novosti/popis-mpc.html";
        private readonly string _baseUrl = "https://www.kaufland.hr";
        private readonly int _downloadDays = 30;

        public KauflandJob(ILogger<KauflandJob> Logger, IDbConnectionFactory DbConnFactory, RetailersHelper RetailersHelper) : base(Logger, DbConnFactory, RetailersHelper, jobName, 1440, 5)
        {
            _logger = Logger;
        }

        public override async Task Work()
        {
            try
            {
                RetailerDataPOCO retailerData = await GetRetailerBasicDataAsync(RetailersEnum.KAUFLAND);

                List<RetailerBusinessUnitPOCO> stores = await GetStoresAsync(retailerData.retailerId);

                List<ImportLog> importLogs = new List<ImportLog>();

                if (stores.Any())
                {
                    var storesDownloads = await GetStoresDownloadsAsync();

                    foreach (var store in stores)
                    {
                        var storeDownloads = storesDownloads.Where(x => x.label.StartsWith(store.filename, StringComparison.InvariantCultureIgnoreCase)).TakeLast(_downloadDays);

                        string downloadUrl = ExtractTodaysDownloadUrl(storeDownloads);

                        if (!string.IsNullOrEmpty(downloadUrl))
                        {
                            await DownloadCSVAsync(_baseUrl + downloadUrl, store.retailerID, store.unitID, retailerData.csvDirectory);

                            importLogs.Add(new ImportLog { retailerID = store.retailerID, unitID = store.unitID });
                        }
                    }

                    await InsertImportLogs(importLogs);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }


        public async Task<IEnumerable<Store>> GetStoresDownloadsAsync() 
        {
            IEnumerable<Store> result = new List<Store>();
             
            using (var playwright = await Playwright.CreateAsync())
            {
                await using (var browser = await playwright.Chromium.LaunchAsync(new()
                {
                    Headless = true,
                    Channel = "msedge"
                }))
                {
                    var context = await browser.NewContextAsync();
                    var page = await context.NewPageAsync();

                    await page.GotoAsync(_basePageUrl);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                    var assetList = page.Locator("div[data-component='AssetList']");
                    string dataProps = await assetList.GetAttributeAsync("data-props");
                    var vueProps = JsonSerializer.Deserialize<VueProps>(dataProps);
                    string jsonPath = vueProps?.settings?.dataUrlAssets;

                    var jsonUrl = new Uri(new Uri(_baseUrl), jsonPath).ToString();
                    var response = await page.APIRequest.GetAsync(jsonUrl);

                    if (response.Ok)
                    {
                        string jsonContent = await response.TextAsync();

                        if (!String.IsNullOrEmpty(jsonContent))
                        {
                            result = JsonSerializer.Deserialize<IEnumerable<Store>>(jsonContent);

                        }
                    }
                }
            }

            return result;
        }

        public string ExtractTodaysDownloadUrl(IEnumerable<Store> storeDownloads, double? day = 0)
        {
            string downloadUrl = string.Empty;
            var currentDate = DateTime.Today.AddDays(-day.Value).ToString("ddMMyyyy");

            for (int i = _downloadDays - 1; i >= 0; i--)
            {
                var storeDownload = storeDownloads.ElementAt(i);

                if (storeDownload.path.Contains(currentDate))
                {
                    downloadUrl = storeDownload.path;
                }
                else
                {
                    continue;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                if (day < _downloadDays)
                {
                    day++;
                    downloadUrl = ExtractTodaysDownloadUrl(storeDownloads, day);
                }
                
            }

            return downloadUrl;
        }
    }
}
