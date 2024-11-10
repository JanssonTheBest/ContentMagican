using ContentMagican.Models;
using ContentMagican.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{

    public class TasksController : Controller
    {
        TaskService _taskService;

        public TasksController(TaskService taskService)
        {
            _taskService = taskService;
        }


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

        [HttpPost]
        public IActionResult CreateRedditVideoAutomationTask(
           string videoDimensions,
           bool verticalResolution,
           string textStyle,
           string gameplayVideo,
           string platform,
           int videoLengthFrom,
           int videoLengthTo,
           string videoTitle)
        {
            // Validate the parameters here if needed
            if (string.IsNullOrWhiteSpace(videoTitle))
            {
                ModelState.AddModelError("videoTitle", "Video title is required.");
            }

            if (!ModelState.IsValid)
            {
                return View();
            }




           
            return RedirectToAction("Main"); 
        }
    }
}

