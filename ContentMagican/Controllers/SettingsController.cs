using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{
    public class SettingsController : Controller
    {
        public IActionResult Main()
        {
            return View();
        }
    }
}
