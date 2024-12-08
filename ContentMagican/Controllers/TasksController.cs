using ContentMagican.DTOs;
using ContentMagican.Models;
using ContentMagican.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ContentMagican.Controllers
{

    public class TasksController : Controller
    {
        TaskService _taskService;
        private readonly IWebHostEnvironment _env;


        public TasksController(TaskService taskService, IWebHostEnvironment env)
        {
            _env = env;
            _taskService = taskService;
        }


        public async Task<IActionResult> Main()
        {

            var list = (await _taskService.GetUsersTasks(HttpContext));
            list.RemoveAll(a => a.Status == (int)TaskService.TaskStatus.deleted);

            return View(new TasksViewModel()
            {
                Tasks = list
            });
        }

        [HttpGet]
        public IActionResult DeleteTasks(long taskId)
        {
            _taskService.DeleteUserTask(HttpContext, taskId).GetAwaiter().GetResult();
            return RedirectToAction("Main", "Tasks");
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
            // Define allowed extensions for fonts, videos, and audios
            string[] fontExtensions = { ".woff", ".woff2", ".ttf", ".otf" };
            string[] videoExtensions = { ".mp4", ".avi", ".mov", ".wmv", ".flv" };
            string[] audioExtensions = { ".mp3", ".wav", ".aac", ".ogg", ".flac" };

            // Define folders for fonts, videos, and audios
            string fontsFolder = Path.Combine(_env.WebRootPath, "MediaResources", "Fonts");
            string videosFolder = Path.Combine(_env.WebRootPath, "MediaResources", "Videos");
            string audiosFolder = Path.Combine(_env.WebRootPath, "MediaResources", "Audios");

            // Enumerate font files
            var fonts = Directory.EnumerateFiles(fontsFolder)
                                 .Where(file => fontExtensions.Contains(Path.GetExtension(file).ToLower()))
                                 .Select(file => new FontDto
                                 {
                                     Name = Path.GetFileNameWithoutExtension(file),
                                     Path = "/MediaResources/Fonts/" + Path.GetFileName(file)
                                 })
                                 .ToList();

            // Enumerate video files
            var videos = Directory.EnumerateFiles(videosFolder)
                                  .Where(file => videoExtensions.Contains(Path.GetExtension(file).ToLower()))
                                  .Select(file => new VideoResourceDto
                                  {
                                      Name = Path.GetFileNameWithoutExtension(file),
                                      Path = "/MediaResources/Videos/" + Path.GetFileName(file)
                                  })
                                  .ToList();

            // Enumerate audio files
            var audios = Directory.EnumerateFiles(audiosFolder)
                                  .Where(file => audioExtensions.Contains(Path.GetExtension(file).ToLower()))
                                  .Select(file => new AudioResourceDto
                                  {
                                      Name = Path.GetFileNameWithoutExtension(file),
                                      Path = "/MediaResources/Audios/" + Path.GetFileName(file)
                                  })
                                  .ToList();

            // Create the ViewModel with fonts, videos, and audios
            var viewModel = new RedditVideoAutomationSettingsViewModel
            {
                fonts = fonts,
                video = videos,
                audio = audios
            };

            return View("RedditVideoAutomationSettings", viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> CreateRedditVideoAutomationTask(
    string videoDimensions,
    bool verticalResolution,
    string textStyle,
    string gameplayVideo,
    string platform,
    int videoLengthFrom,
    int videoLengthTo,
    int videosPerDay,
    string taskDescription)
        {
         

            // Validate videosPerDay
            if (videosPerDay < 1)
            {
                ModelState.AddModelError("videosPerDay", "Please select a valid number of videos per day.");
            }


            await _taskService.CreateRedditVideoAutomationTask(videoDimensions,
     verticalResolution,
     textStyle,
     gameplayVideo,
     platform,
     videoLengthFrom,
     videoLengthTo,
     videosPerDay, // Added this parameter
    taskDescription,
     HttpContext);

            return RedirectToAction("Main", "Tasks");
        }

    }
}

