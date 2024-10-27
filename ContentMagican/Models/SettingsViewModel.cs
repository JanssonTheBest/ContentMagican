using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Models
{
    public class SettingsViewModel : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
