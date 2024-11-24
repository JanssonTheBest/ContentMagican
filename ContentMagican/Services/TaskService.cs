using ContentMagican.Database;

namespace ContentMagican.Services
{
    public class TaskService
    {
        ApplicationDbContext _applicationDbContext;
        UserService _userService;
        FFmpegService _ffmpegService;
        public TaskService(ApplicationDbContext applicationDbContext, UserService userService, FFmpegService ffmpegService)
        {
            _applicationDbContext = applicationDbContext;
            _userService = userService;
            _ffmpegService = ffmpegService;
        }

        public enum TaskTypes
        {
            Video_Automation,
            Blog_Automation
        }

        public enum TaskSubTypes
        {
            Reddit_Stories,
            AI_Content
        }

        public enum TaskStatus
        {
            active,
            inactive,
        }

        public async Task CreateRedditVideoAutomationTask(string videoDimensions,
           bool verticalResolution,
           string textStyle,
           string gameplayVideo,
           string platform,
           int videoLengthFrom,
           int videoLengthTo,
           int videosPerDay,
           string videoTitle,
           string subReddit,
           HttpContext ctx)
        {

            var user = await _userService.RetrieveUserInformation(ctx);
            var task = new _Task()
            {
                Created = DateTime.UtcNow,
                Description = videoTitle,
                Status = (int)TaskStatus.active,
                Type = (int)TaskTypes.Video_Automation,
                Subtype = (int)TaskSubTypes.Reddit_Stories,
                UserId = user.Id,
                AdditionalInfo = subReddit
            };


            await _applicationDbContext.Task.AddAsync(task);
            await _applicationDbContext.SaveChangesAsync();

            await _applicationDbContext.VideoAutomation.AddAsync(new VideoAutomation()
            {
                FFmpegString = await _ffmpegService.CreateFFmpegStringFromParameters(verticalResolution,textStyle,gameplayVideo,videoTitle),
                Interval = videosPerDay,
                TaskId = task.Id,
            });

            await _applicationDbContext.SaveChangesAsync();
        }


        public async Task<List<_Task>> GetUsersTasks(HttpContext ctx)
        {
            var user = await _userService.RetrieveUserInformation(ctx);
            var tasks = _applicationDbContext.Task.Where(a => a.UserId == user.Id).ToList();
            return tasks;
        }

        public async Task DeleteUserTask(HttpContext ctx, long taskId)
        {
            var user = await _userService.RetrieveUserInformation(ctx);
            var task = _applicationDbContext.Task.Where(a => a.UserId == user.Id && a.Id == taskId).FirstOrDefault();

            if(task == default)
            {
                return;
            }

            _applicationDbContext.Task.Remove(task);
            await _applicationDbContext.SaveChangesAsync();
        }

        public async Task<VideoAutomation> GetVideoAutomationInfo(long taskId)
        {
            return _applicationDbContext.VideoAutomation.Where(a => a.TaskId == taskId).FirstOrDefault();
        }

    }
}

