using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{
    public class InfoController : Controller
    {
        public IActionResult Privacy()
        {
            return View();
        }

        public IActionResult Terms()
        {
            return View();
        }
    }
}
