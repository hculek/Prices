using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Prices.WindowsService.Database;
using Prices.WindowsService.Helpers;
using Prices.WindowsService.POCO;
using System.IO.Compression;

namespace Prices.WindowsService.CSV_Jobs
{
    public class LidlJob : Base<LidlJob>
    {
        private static readonly string jobName = "LidlJob";
        private readonly ILogger<LidlJob> _logger;
        private readonly string _basePageUrl = "https://tvrtka.lidl.hr/cijene";

        public LidlJob(ILogger<LidlJob> Logger, IDbConnectionFactory DbConnFactory, RetailersHelper RetailersHelper) : base(Logger, DbConnFactory, RetailersHelper, jobName, 1440, 5)
        {
            _logger = Logger;
        }

        public override async Task Work()
        {
            try
            {
                RetailerDataPOCO retailerData = await GetRetailerBasicDataAsync(RetailersEnum.LIDL);

                List<RetailerBusinessUnitPOCO> stores = await GetStoresAsync(retailerData.retailerId);

                if (stores.Any())
                {
                    HtmlDocument? doc = await GetWebDocAsync(_basePageUrl);

                    string todaysDate = DateTime.Now.ToString("dd.MM.yyyy.");

                    var zipUrl = doc.DocumentNode.Descendants("p")
                            .Where(x => x.InnerText.Contains($"Cijene u trgovinama koje vrijede na dan {todaysDate}", StringComparison.InvariantCultureIgnoreCase))
                            .FirstOrDefault().Descendants("a").FirstOrDefault().Attributes["href"].Value;


                    if (!String.IsNullOrEmpty(zipUrl))
                    {
                        List<ImportLog> importLogs = new List<ImportLog>();

                        using (ZipArchive zip = await DownloadZipAsync(zipUrl))
                        {
                            foreach (var store in stores)
                            {
                                var csv = zip.Entries.FirstOrDefault(x => x.Name.StartsWith(store.filename, StringComparison.InvariantCultureIgnoreCase));
                                if (csv != null)
                                {
                                    await SaveCsvFromZip(csv, store.retailerID, store.unitID, retailerData.csvDirectory);

                                    importLogs.Add(new ImportLog { retailerID = store.retailerID, unitID = store.unitID });
                                }
                            }
                        }

                        await InsertImportLogs(importLogs);
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
