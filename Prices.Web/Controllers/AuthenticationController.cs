using Microsoft.AspNetCore.Mvc;

namespace Prices.Web.Controllers
{
    public class AuthenticationController : Controller
    {
        public IActionResult Login()
        {
            return View();
        }
    }
}
