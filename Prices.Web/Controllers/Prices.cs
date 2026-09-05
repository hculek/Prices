using Microsoft.AspNetCore.Mvc;

namespace Prices.Web.Controllers
{
    public class Prices : Controller
    {
        public IActionResult PricesList()
        {
            return View();
        }

        public IActionResult Following()
        {
            return View();
        }



        [HttpGet]
        public async Task<IActionResult> GetPricesList()
        {
            var data = new[]
            {
                new { barcode = "3858881234567", product = "Mlijeko 1L", price = 1.25, retailer = "Konzum", retailerunit = "Konzum Virovitica Centar" },
                new { barcode = "3858885671234", product = "Kruh polubijeli 500g", price = 0.89, retailer = "Lidl", retailerunit = "Lidl Virovitica" },
                new { barcode = "3858889871234", product = "Jaja M 10kom", price = 2.49, retailer = "Plodine", retailerunit = "Plodine Virovitica" },
                new { barcode = "3858882221111", product = "Maslac 200g", price = 1.99, retailer = "Konzum", retailerunit = "Konzum Virovitica Centar" },
                new { barcode = "3858883332222", product = "Jogurt prirodni 180g", price = 0.65, retailer = "Spar", retailerunit = "Spar Virovitica" },
                new { barcode = "3858884443333", product = "Sir edamac 300g", price = 3.29, retailer = "Lidl", retailerunit = "Lidl Virovitica" },
                new { barcode = "3858885554444", product = "Šunka pileća 200g", price = 1.79, retailer = "Plodine", retailerunit = "Plodine Virovitica" },
                new { barcode = "3858886665555", product = "Tjestenina 500g", price = 0.99, retailer = "Konzum", retailerunit = "Konzum Virovitica Centar" },
                new { barcode = "3858887776666", product = "Riža 1kg", price = 1.49, retailer = "Spar", retailerunit = "Spar Virovitica" },
                new { barcode = "3858888887777", product = "Ulje suncokretovo 1L", price = 2.19, retailer = "Lidl", retailerunit = "Lidl Virovitica" }
            };

            return Json(new { data });
        }
    }
}
