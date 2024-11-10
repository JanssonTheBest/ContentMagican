using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{

    [Authorize]
    public class SettingsController : Controller
    {
        public IActionResult Main()
        {
            return View();
        }
    }
}
