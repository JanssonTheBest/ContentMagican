using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{
    public class TasksController : Controller
    {
        public IActionResult Main()
        {
            return View();
        }
    }
}
