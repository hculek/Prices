using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using NLog;
using Npgsql;
using Prices.WindowsService.Database;
using Prices.WindowsService.POCO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prices.WindowsService.CSV_Jobs
{
    public class KonzumJob : Base<KonzumJob>
    {
        private readonly ILogger<KonzumJob> _logger;

        private readonly string _basePageUrl = "https://www.konzum.hr/cjenici?page=";
        private readonly string _baseDownloadUrl = "https://www.konzum.hr";

        public KonzumJob(ILogger<KonzumJob> Logger, IDbConnectionFactory DbConnFactory) : base(Logger, DbConnFactory, "KonzumJob", 1440, 5)
        {
            _logger = Logger;
        }

        public override async Task Work()
        {
            bool finished = false;
            int page = 1;

            var stores = await GetStoresAsync(1);

            if (stores.Any()) 
            {
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
                        //todo saveLocation url
                        await DownloadCSVAsync(_baseDownloadUrl+downloadData.hrefDownload, store.retailerID, store.unitID, "K:\\Aplikacije\\Cjenici\\CSV_DUMP\\KONZUM");
                    }
                }
            }
        }     
    }
}
