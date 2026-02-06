using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Prices.WindowsService.Database;
using Prices.WindowsService.Helpers;

namespace Prices.WindowsService.CSV_Jobs
{
    public class KTCJob : Base<KTCJob>
    {
        private static readonly string jobName = "KTCJob";
        private readonly ILogger<KTCJob> _logger;
        private readonly string _basePageUrl = "https://www.ktc.hr/cjenici?poslovnica=";
        private readonly string _baseDownloadUrl = "https://www.ktc.hr";

        public KTCJob(ILogger<KTCJob> Logger, IDbConnectionFactory DbConnFactory, RetailersHelper RetailersHelper) : base(Logger, DbConnFactory, RetailersHelper, jobName, 1440, 5)
        {
            _logger = Logger;
        }

        public override async Task Work()
        {
            try
            {
                var retailerData = await GetRetailerBasicDataAsync(RetailersEnum.KTC);

                var stores = await GetStoresAsync(retailerData.retailerId);

                if (stores.Any())
                {
                    foreach (var store in stores)
                    {
                        string _pageUrl = _basePageUrl + store.lookup;

                        HtmlDocument? doc = await GetWebDocAsync(_pageUrl);

                        var downloadHref = doc.DocumentNode.Descendants("li").LastOrDefault().FirstChild.Attributes["href"].Value;

                        await DownloadCSVAsync(_baseDownloadUrl + downloadHref, store.retailerID, store.unitID, retailerData.csvDirectory);
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
