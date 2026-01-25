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

        private readonly string _baseUrl = "https://www.konzum.hr/cjenici?page=";
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
                while (!finished)
                {
                    string _pageUrl = _baseUrl + page;

                    HtmlDocument? doc = await GetWebDocAsync(_pageUrl);

                    var downloadElement = doc.DocumentNode.Descendants("section")
                    .Where(node => node.GetAttributeValue("class", "").Contains("py-1")).FirstOrDefault();

                    if (downloadElement == null)
                    {
                        finished = true;
                    }

                    var downloadUrls = downloadElement.Descendants("a")
                    .Where(node => node.GetAttributeValue("href", "").Contains("/cjenici/download"));

                    //TODO continue

                    page++;
                }
            }
        }     
    }
}
