using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{
    [Authorize]
    public class PlanController : Controller
    {
        public IActionResult Main()
        {
            return View();
        }
    }
}
