using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Main()
        {
            return View();
        }
    }
}
