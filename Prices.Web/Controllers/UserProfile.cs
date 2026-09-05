using Microsoft.AspNetCore.Mvc;

namespace Prices.Web.Controllers
{
    public class UserProfile : Controller
    {
        public IActionResult UserPanel()
        {
            return View();
        }
    }
}
