using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{

    public class DashboardController : Controller
    {

        public DashboardController()
        {
        }

        public async Task<IActionResult> Main()
        {

            var debug = User.Claims;
            return View();
        }
    }
}
