using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Prices.WindowsService.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prices.WindowsService.CSV_Jobs
{
    public class KTCJob : Base<KTCJob>
    {
        private readonly ILogger<KTCJob> _logger;
        private readonly string _basePageUrl = "https://www.ktc.hr/cjenici?poslovnica=";
        private readonly string _baseDownloadUrl = "https://www.ktc.hr";

        public KTCJob(ILogger<KTCJob> Logger, IDbConnectionFactory DbConnFactory) : base(Logger, DbConnFactory, "KTCJob", 1440, 5)
        {
            _logger = Logger;
        }

        public override async Task Work()
        {
            var stores = await GetStoresAsync(2);

            if (stores.Any())
            {
                foreach (var store in stores)
                {
                    string _pageUrl = _basePageUrl + store.lookup;

                    HtmlDocument? doc = await GetWebDocAsync(_pageUrl);

                    var downloadHref = doc.DocumentNode.Descendants("li").LastOrDefault().FirstChild.Attributes["href"].Value;

                    //todo saveLocation url
                    await DownloadCSVAsync(_baseDownloadUrl + downloadHref, store.retailerID, store.unitID, "K:\\Aplikacije\\Cjenici\\CSV_DUMP\\KTC");
                }
            }

        }
    }
}
