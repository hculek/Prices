using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Prices.WindowsService.Database;
using System.IO.Compression;

namespace Prices.WindowsService.CSV_Jobs
{
    public class LidlJob : Base<LidlJob>
    {
        private readonly ILogger<LidlJob> _logger;
        private readonly string _basePageUrl = "https://tvrtka.lidl.hr/cijene";

        public LidlJob(ILogger<LidlJob> Logger, IDbConnectionFactory DbConnFactory) : base(Logger, DbConnFactory, "LidlJob", 1440, 5)
        {
            _logger = Logger;
        }

        public override async Task Work()
        {
            try
            {
                var stores = await GetStoresAsync(3); //todo id from enum

                if (stores.Any())
                {
                    HtmlDocument? doc = await GetWebDocAsync(_basePageUrl);

                    var zipUrl = doc.DocumentNode.Descendants("p")
                        .Where(x => x.InnerText.Contains("Cijene u trgovinama koje", StringComparison.InvariantCultureIgnoreCase))
                        .LastOrDefault().Descendants("a").FirstOrDefault().Attributes["href"].Value;


                    using (ZipArchive zip = await DownloadZipAsync(zipUrl))
                    {
                        foreach (var store in stores)
                        {
                            var csv = zip.Entries.FirstOrDefault(x => x.Name.StartsWith(store.filename, StringComparison.InvariantCultureIgnoreCase));
                            if (csv != null)
                            {
                                await SaveCsvFromZip(csv, store.retailerID, store.unitID, store.csvDirectory);
                            }
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
