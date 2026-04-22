using DocumentFormat.OpenXml.Wordprocessing;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Prices.WindowsService.Database;
using Prices.WindowsService.Helpers;
using Prices.WindowsService.POCO;
using System.IO.Compression;

namespace Prices.WindowsService.CSV_Jobs
{
    public class PlodineJob : Base<PlodineJob>
    {
        private static readonly string jobName = "PlodineJob";
        private readonly ILogger<PlodineJob> _logger;
        private readonly string _basePageUrl = "https://www.plodine.hr/info-o-cijenama";
        public PlodineJob(ILogger<PlodineJob> Logger, IDbConnectionFactory DbConnFactory, RetailersHelper RetailersHelper) 
            : base(Logger, DbConnFactory, RetailersHelper, jobName)
        {
            _logger = Logger;
        }

        public override async Task Work()
        {
            try
            {
                RetailerDataPOCO retailerData = await GetRetailerBasicDataAsync(RetailersEnum.PLODINE);

                List<RetailerBusinessUnitPOCO> stores = await GetStoresAsync(retailerData.retailerId);

                if (stores.Any())
                {
                    HtmlDocument? doc = await GetWebDocAsync(_basePageUrl);

                    string todaysDate = DateTime.Now.ToString("dd_MM_yyyy");

                    var zipUrl = doc.DocumentNode.Descendants("a")
                        .Select(node => node.GetAttributeValue("href", "")).FirstOrDefault(node => node.Contains(todaysDate));

                    if (!string.IsNullOrEmpty(zipUrl))
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


                        if (importLogs.Any())
                        {
                            await InsertImportLogs(importLogs);
                            SetSleepMinutes(1440);
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
