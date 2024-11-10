using ContentMagican.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{

    public class DashboardController : Controller
    {

        JwtAuthorizationHandler _jwtAuthorizationHandler;

        public DashboardController(JwtAuthorizationHandler jwtAuthorizationHandler)
        {
            _jwtAuthorizationHandler = jwtAuthorizationHandler;
        }

        public async Task<IActionResult> Main()
        {

            var debug = User.Claims;
            return View();
        }
    }
}
