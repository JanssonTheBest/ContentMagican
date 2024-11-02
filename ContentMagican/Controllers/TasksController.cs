using ContentMagican.Models;
using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{
    public class TasksController : Controller
    {

        public IActionResult Main()
        {
            return View(new TasksViewModel());
        }

        [HttpGet]
        public IActionResult DeleteTasks(long taskId)
        {

            return View();
        }

        [HttpGet]
        public IActionResult CreateTask()
        {

            return View();
        }

        [HttpPost]
        public IActionResult ChoseTaskType(string r)
        {

            return View("ChoseVideoAutomationContentType");
        }

        [HttpPost]
        public IActionResult ChangeTaskSettings(string r)
        {

            return View("RedditVideoAutomationSettings");
        }
    }
}
