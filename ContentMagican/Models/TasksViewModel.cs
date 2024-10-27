using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Models
{
    public class TasksViewModel : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
