using ContentMagican.Database;

namespace ContentMagican.Services
{
    public class TaskService
    {
        ApplicationDbContext _applicationDbContext;
        public TaskService(ApplicationDbContext applicationDbContext)
        {
            _applicationDbContext = applicationDbContext;
        }



        public enum TaskTypes
        {
            VideoAutomation
        }

        public enum TaskSubTypes
        {
            RedditVideoAutomatior
        }

        public async Task CreateRedditVideoAutomationTask(string videoDimensions,
           bool verticalResolution,
           string textStyle,
           string gameplayVideo,
           string platform,
           int videoLengthFrom,
           int videoLengthTo,
           string videoTitle)
        {
            await _applicationDbContext.Task.AddAsync()
        }


    }
}
