namespace Prices.WindowsService.POCO
{
    public class variousPOCO
    {
    }

    public class ImportLog
    {
        public int retailerID { get; set; }
        public int unitID { get; set; }
    }

    public class DownloadsDataPOCO
    {
        public string innerHtml { get; set; }
        public string outerHtml { get; set; }
        public string hrefDownload { get; set; }
    }


    public class VueProps
    {
        public Settings settings { get; set; }
    }

    public class Settings
    {
        public string dataUrlAssets { get; set; }
    }


    public class Store
    {
        public string label { get; set; }
        public string path { get; set; }
    }

}
