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
            _taskService.DeleteUserTask(HttpContext,taskId).GetAwaiter().GetResult();
            return RedirectToAction("Main","Tasks");
        }

        [HttpGet]
        public IActionResult CreateTask()
        {

            return View();
        }

    //    [HttpGet]
    //    public async Task<IActionResult> EditTasks(long taskId)
    //    {

    //        var tasks = await _taskService.GetUsersTasks(HttpContext);

    //        var task = tasks.Where(a => a.Id == taskId).FirstOrDefault();

    //        if(task == default)
    //        {
    //            return RedirectToAction("CreateTask", "Tasks");
    //        }

    //        var videoAutomation = await _taskService.GetVideoAutomationInfo(taskId);

    //        return View("EdditTaskRedditVideoAutomationSettings", new EditTaskViewModel()
    //        {
    //            AdditionalInfo = task.AdditionalInfo,
    //            GameplayVideo = videoAutomation.FFmpegString,
    //            TextStyle = videoAutomation.FFmpegString,
    //            Platform = "",
    //            VideoDimensions = videoAutomation.FFmpegString,
    //            VerticalResolution = true,
    //            VideosPerDay = videoAutomation.Interval,
    //            TaskId = taskId,
    //            VideoLengthFrom = 1,
    //            VideoLengthTo = 5,
    //            VideoTitle = videoAutomation.FFmpegString
    //        });

           
    //    }

    //    [HttpGet]
    //    public IActionResult SubmitEditTasks(
    //         string videoDimensions,
    //bool verticalResolution,
    //string textStyle,
    //string gameplayVideo,
    //string platform,
    //int videoLengthFrom,
    //int videoLengthTo,
    //int videosPerDay,
    //string subreddit,
    //string videoTitle,
    //        long taskId
    //        )
    //    {



    //        return RedirectToAction("Main", "Tasks");
           
    //    }

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
        public async Task<IActionResult> CreateRedditVideoAutomationTask(
    string videoDimensions,
    bool verticalResolution,
    string textStyle,
    string gameplayVideo,
    string platform,
    int videoLengthFrom,
    int videoLengthTo,
    int videosPerDay,
    string subreddit,
    string videoTitle)
        {
            // Validate the parameters here if needed
            if (string.IsNullOrWhiteSpace(videoTitle))
            {
                ModelState.AddModelError("videoTitle", "Video title is required.");
            }

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
     videoTitle,
     subreddit,
     HttpContext);

            return RedirectToAction("Main", "Tasks");
        }

    }
}

