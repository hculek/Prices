using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prices.WindowsService.POCO
{
    public class RetailerBusinessUnitPOCO
    {
        public int retailerID { get; set; }
        public int unitID { get; set; }

        public string? lookup { get; set; }
        public string? filename { get; set; }
        public string csvDirectory { get; set; }
    }
}
