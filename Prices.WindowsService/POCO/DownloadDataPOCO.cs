using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prices.WindowsService.POCO
{
    public class DownloadDataPOCO
    {
        public string innerHtml { get; set; }
        public string outerHtml { get; set; }
        public string hrefDownload{ get; set; }
    }
}
