using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{
    public class PlanController : Controller
    {
        public IActionResult Main()
        {
            return View();
        }
    }
}
