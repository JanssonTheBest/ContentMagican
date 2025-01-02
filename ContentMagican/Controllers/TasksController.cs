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
        UserService _userService;

        public TasksController(TaskService taskService, IWebHostEnvironment env, UserService userService)
        {
            _env = env;
            _taskService = taskService;
            _userService = userService;
        }


        public async Task<IActionResult> Main()
        {

            var list = (await _taskService.GetUsersTasks(HttpContext));
            list.RemoveAll(a => a.Status == (int)TaskService.TaskStatus.deleted);


            foreach (var task in list)
            {
                var session = await _taskService.GetSocialMediaAccessSession(task.SocialMediaAccessSessionsId);

                if (session != null)
                {
                    task.AdditionalInfo = $"{session.UserName},{session.AvatarUrl},{session.socialmedia_name}";
                }

            }

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

        [HttpGet]
        public async Task<IActionResult> ChangeTaskSettings(string r)
        {
            var type = (TaskService.TaskSubTypes)Convert.ToInt32(r);

            switch (type)
            {
                case TaskService.TaskSubTypes.Reddit_Stories:
                    return await RedditVideoAutomationSettings();

                case TaskService.TaskSubTypes.Dark_Psychology:
                    return await DarkPsychologyVideoAutomationSettings();

                    default:return BadRequest();
            }
        }


        public async Task<IActionResult> DarkPsychologyVideoAutomationSettings()
        {

            var ss = await _userService.RetrieveActiveUserSocialMediaAccessSessions(HttpContext);
            var viewModel = new DarkPsychologyVideoAutomationSettingsModel
            {
                accounts = ss.OrderBy(a => a.CreatedAt).ToList(),
            };
            return View("DarkPsychologyVideoAutomationSettings", viewModel);
        }


        public async Task<IActionResult> RedditVideoAutomationSettings()
        {
            // Define allowed extensions for fonts, videos, and audios
            string[] fontExtensions = { ".woff", ".woff2", ".ttf", ".otf" };
            string[] videoExtensions = { ".mp4", ".avi", ".mov", ".wmv", ".flv" };
            string[] audioExtensions = { ".mp3", ".wav", ".aac", ".ogg", ".flac" };

            // Define folders for fonts, videos, and audios
            string fontsFolder = Path.Combine(_env.WebRootPath, "MediaResources", "Fonts");
            string videosFolder = Path.Combine(_env.WebRootPath, "MediaResources", "Videos", "BackgroundDistraction");
            string audiosFolder = Path.Combine(_env.WebRootPath, "MediaResources", "Audios");

           

            // Enumerate video files
            var videos = Directory.EnumerateFiles(videosFolder)
                                  .Where(file => videoExtensions.Contains(Path.GetExtension(file).ToLower()))
                                  .Select(file => new VideoResourceDto
                                  {
                                      Name = Path.GetFileNameWithoutExtension(file),
                                      Path = Path.GetFileName(file)
                                  })
                                  .ToList();

            // Enumerate audio files
            var audios = Directory.EnumerateFiles(audiosFolder)
                                  .Where(file => audioExtensions.Contains(Path.GetExtension(file).ToLower()))
                                  .Select(file => new AudioResourceDto
                                  {
                                      Name = Path.GetFileNameWithoutExtension(file),
                                      Path = Path.GetFileName(file)
                                  })
                                  .ToList();

            // Create the ViewModel with fonts, videos, and audios
            var ss = await _userService.RetrieveActiveUserSocialMediaAccessSessions(HttpContext);
            var viewModel = new RedditVideoAutomationSettingsViewModel
            {
                accounts = ss.OrderBy(a => a.CreatedAt).ToList(),
                video = videos,
                audio = audios
            };

            return View("RedditVideoAutomationSettings", viewModel);
        }


        [HttpPost]
        public async Task<IActionResult> CreateRedditVideoAutomationTask(
    string AccountToPublish,
    string TextStyle,
    string GameplayVideo,
    int VideosPerDay,
    string taskDescription, string storyGenre)

        {
            

            // Validate videosPerDay
            if (VideosPerDay < 1)
            {
                ModelState.AddModelError("videosPerDay", "Please select a valid number of videos per day.");
            }

            var contentcreations = await _taskService.AvailableContentCreations(HttpContext);

            if(contentcreations < VideosPerDay)
            {
                return BadRequest("You have used more content creations than available for your current plan, when creating a task ensure that videos/day is within the content-creations amount available.");
            }

            if (int.TryParse(AccountToPublish, out var socialMediaAccesSessionId))
            {
                await _taskService.CreateRedditVideoAutomationTask(
 TextStyle,
 GameplayVideo,
 socialMediaAccesSessionId,
 VideosPerDay, // Added this parameter
taskDescription,
storyGenre,
 HttpContext);
                return RedirectToAction("Main", "Tasks");

            }
            else
            {
                return BadRequest("wrong id");
            }
        }


        [HttpPost]
        public async Task<IActionResult> CreateDarkPsychologyVideoAutomationTask(
    string AccountToPublish,
    string TextStyle,
    string GameplayVideo,
    int VideosPerDay,
    string taskDescription, string storyGenre)

        {


            // Validate videosPerDay
            if (VideosPerDay < 1)
            {
                ModelState.AddModelError("videosPerDay", "Please select a valid number of videos per day.");
            }

            var contentcreations = await _taskService.AvailableContentCreations(HttpContext);

            if (contentcreations < VideosPerDay)
            {
                return BadRequest("You have used more content creations than available for your current plan, when creating a task ensure that videos/day is within the content-creations amount available.");
            }


            if (int.TryParse(AccountToPublish, out var socialMediaAccesSessionId))
            {
                await _taskService.CreateDarkPsychologyVideoAutomationTask(
 TextStyle,
 socialMediaAccesSessionId,
 VideosPerDay, // Added this parameter
taskDescription,
null,
 HttpContext);
                return RedirectToAction("Main", "Tasks");

            }
            else
            {
                return BadRequest("wrong id");
            }
        }

    }
}

