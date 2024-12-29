using ContentMagican.Database;
using ContentMagican.DTOs;
using Stripe.Identity;
using System.Text.Json;
using static ContentMagican.Services.TaskService;

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

        public static List<TaskVideoAutomationFrontendRepresentation> videoAutomationAlternatives = new()
{
    new TaskVideoAutomationFrontendRepresentation()
    {
        type = TaskSubTypes.Reddit_Stories,
        iframeUrl = "<iframe width=\"75\" height=\"132\" src=\"https://www.youtube.com/embed/6nUspSoNW_8?autoplay=1&mute=1&playsinline=1&controls=0&loop=1&playlist=6nUspSoNW_8\" title=\"How Did You Lose It #askreddit #shorts\" frameborder=\"0\" style=\"border:none; overflow:hidden;\" referrerpolicy=\"strict-origin-when-cross-origin\" allow=\"autoplay\" allowfullscreen></iframe>"
    },
    new TaskVideoAutomationFrontendRepresentation()
    {
        type = TaskSubTypes.Dark_Psychology,
        iframeUrl = "<iframe width=\"75\" height=\"132\" src=\"https://www.youtube.com/embed/0ZLU6xpaNTk?autoplay=1&mute=1&playsinline=1&controls=0&loop=1&playlist=0ZLU6xpaNTk\" title=\"6 dark manipulation tricks #manipulation\" frameborder=\"0\" style=\"border:none; overflow:hidden;\" referrerpolicy=\"strict-origin-when-cross-origin\" allow=\"autoplay\" allowfullscreen></iframe>"
    }
};


        public enum TaskTypes
        {
            Video_Automation,
            //Blog_Automation
        }

        public enum TaskSubTypes
        {
            Reddit_Stories,
            Dark_Psychology,
            //AI_Content
        }

        public enum TaskStatus
        {
            active,
            inactive,
            deleted,
        }

        public async Task CreateRedditVideoAutomationTask(
    string TextStyle,
    string GameplayVideo,
    int socialMediaAccessSessionId,
    int VideosPerDay,
    string taskDescription,
    string aditionalInfo,

           HttpContext ctx)
        {

            var user = await _userService.RetrieveUserInformation(ctx);
            var task = new _Task()
            {
                Created = DateTime.UtcNow,
                Description = taskDescription ?? "",
                Status = (int)TaskStatus.active,
                Type = (int)TaskTypes.Video_Automation,
                Subtype = (int)TaskSubTypes.Reddit_Stories,
                UserId = user.Id,
                AdditionalInfo = "",
                SocialMediaAccessSessionsId = socialMediaAccessSessionId
            };



            await _applicationDbContext.Task.AddAsync(task);
            await _applicationDbContext.SaveChangesAsync();

            await _applicationDbContext.VideoAutomation.AddAsync(new VideoAutomation()
            {
                FFmpegString = await CreateContentInfoJsonObject("RedditStory", Path.Combine("Videos","BackgroundDistraction", GameplayVideo), textStyle: TextStyle, voiceSpeed: 1.2, additionalInfo: aditionalInfo, backgroudAudioVolume:0.2, textToSpeechAudioVolume:1.7, backgroundAudio: "Audios\\Creepy.mp3"),
                Interval = VideosPerDay,
                TaskId = task.Id,
            });

            await _applicationDbContext.SaveChangesAsync();
        }



        public async Task CreateDarkPsychologyVideoAutomationTask(
    string TextStyle,
    int socialMediaAccessSessionId,
    int VideosPerDay,
    string taskDescription,
    string aditionalInfo,

          HttpContext ctx)
        {

            var user = await _userService.RetrieveUserInformation(ctx);
            var task = new _Task()
            {
                Created = DateTime.UtcNow,
                Description = taskDescription ?? "",
                Status = (int)TaskStatus.active,
                Type = (int)TaskTypes.Video_Automation,
                Subtype = (int)TaskSubTypes.Dark_Psychology,
                UserId = user.Id,
                AdditionalInfo = "",
                SocialMediaAccessSessionsId = socialMediaAccessSessionId
            };


            await _applicationDbContext.Task.AddAsync(task);
            await _applicationDbContext.SaveChangesAsync();

            await _applicationDbContext.VideoAutomation.AddAsync(new VideoAutomation()
            {
                FFmpegString = await CreateContentInfoJsonObject("Psychologi", "Videos\\Psychologi\\PsychologiLiebertEdit.mov", textStyle: TextStyle, voiceSpeed: 1.1, additionalInfo: aditionalInfo, backgroundAudio: "Audios\\Psychologi2.mp3", backgroudAudioVolume: 0.3, textToSpeechAudioVolume: 1.7,voice:"echo"),
                Interval = VideosPerDay,
                TaskId = task.Id,
            });

            await _applicationDbContext.SaveChangesAsync();
        }

        public async Task<string> CreateContentInfoJsonObject(string type, string backgroundVideo, string backgroundAudio = null, double backgroudAudioVolume = 1, double textToSpeechAudioVolume = 1, string voice = "onyx", string textStyle = "arial", double voiceSpeed = 1, string additionalInfo = "")
        {
            var dto = new ContentInfo()
            {
                BackgroundAudio = backgroundAudio,
                BackgroundVideo = backgroundVideo,
                Type = type,
                AdditionalInfo = additionalInfo,
                BackgroundAudioVolume = backgroudAudioVolume,
                TextToSpeechVoice = voice,
                TextToSpeechVolume = textToSpeechAudioVolume,
                VoiceSpeed = voiceSpeed,
            };

            return JsonSerializer.Serialize(dto);
        }

        public async Task<List<_Task>> GetUsersTasks(HttpContext ctx)
        {
            var user = await _userService.RetrieveUserInformation(ctx);
            var tasks = _applicationDbContext.Task.Where(a => a.UserId == user.Id);
            return tasks.ToList();
        }

        public async Task<List<_Task>> GetAllActiveTasks()
        {
            var tasks = _applicationDbContext.Task.Where(a => a.Status == 0);
            return tasks.ToList();
        }

        public async Task<SocialMediaAccessSession> GetSocialMediaAccessSession(int socialMediaAccessSessionId)
        {
            var session = _applicationDbContext.SocialMediaAccessSessions.Where(a => a.id == socialMediaAccessSessionId).FirstOrDefault();
            if (session == default)
            {
                return null;
            }

            return session;
        }

        public async Task DeleteUserTask(HttpContext ctx, long taskId)
        {
            var user = await _userService.RetrieveUserInformation(ctx);
            var task = _applicationDbContext.Task.Where(a => a.UserId == user.Id && a.Id == taskId).FirstOrDefault();

            if (task == default)
            {
                return;
            }

            task.Status = (int)TaskStatus.deleted;

            await _applicationDbContext.SaveChangesAsync();
        }

        public async Task<VideoAutomation> GetVideoAutomationInfo(long taskId)
        {
            return _applicationDbContext.VideoAutomation.Where(a => a.TaskId == taskId).FirstOrDefault();
        }

    }

    public class TaskVideoAutomationFrontendRepresentation()
    {
        public TaskSubTypes type;
        public string iframeUrl;
    }
}

