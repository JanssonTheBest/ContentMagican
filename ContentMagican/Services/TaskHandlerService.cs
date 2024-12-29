using ContentMagican.Database;
using ContentMagican.DTOs;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using static System.Formats.Asn1.AsnWriter;

namespace ContentMagican.Services
{
    public class TaskHandlerService : BackgroundService
    {
        private readonly ILogger<TaskHandlerService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly OpenAIService _openAIService;
        //private readonly AzureSpeechService _azureSpeechService;
        private readonly FFmpegService _ffmpegService;


        public TaskHandlerService(
            ILogger<TaskHandlerService> logger,
            IServiceScopeFactory serviceScopeFactory,
            OpenAIService openAIService,
            //AzureSpeechService azureSpeechService,
            FFmpegService ffmpegService)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _openAIService = openAIService;
            //_azureSpeechService = azureSpeechService;
            _ffmpegService = ffmpegService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("TaskHandlerService is starting task processing...");

                    using var scope = _serviceScopeFactory.CreateScope();
                    var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();



                    var tasks = await taskService.GetAllActiveTasks();


                    if (tasks == null || !tasks.Any())
                    {
                        _logger.LogInformation("No tasks found. Waiting before retrying...");
                        await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                        continue;
                    }

                    var processingTasks = tasks.Select(task => ProcessTaskAsync(task, stoppingToken));
                    await Task.WhenAll(processingTasks);

                    _logger.LogInformation("TaskHandlerService completed processing tasks.");
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("TaskHandlerService is stopping due to cancellation.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred in TaskHandlerService.");
                }

                await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
            }
        }

        private async Task ProcessTaskAsync(_Task task, CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var tiktokService = scope.ServiceProvider.GetRequiredService<TiktokService>();

                _logger.LogInformation($"Processing task ID {task.Id}...");
                var appDbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var videoAutomation = appDbContext.VideoAutomation.Where(a => a.TaskId == task.Id).FirstOrDefault();
                var contentInfo = JsonSerializer.Deserialize<ContentInfo>(videoAutomation.FFmpegString);




                string tempId = Guid.NewGuid().ToString();
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string relativePath = Path.Combine(baseDirectory, "temp", "temp", tempId);
                string ttsPath = Path.Combine(relativePath, "tts.mp3");
                Directory.CreateDirectory(relativePath);
                string mediaResourcesPath = Path.Combine(baseDirectory,"wwwroot\\MediaResources");
                string prompt = "";
                string[] tags = Array.Empty<string>();
                switch ((TaskService.TaskSubTypes)task.Subtype)
                {
                    case TaskService.TaskSubTypes.Reddit_Stories:

                        prompt = $"Create a creative and unique first-person story in Reddit style, incorporating {contentInfo.AdditionalInfo}. Ensure it has a distinct voice and approach to avoid similarities with existing stories. (your answer is only alowed to contain title and content formated in json)";
                        tags = new string[] { "#redditstories", "#redditstory" };

                        break;
                    case TaskService.TaskSubTypes.Dark_Psychology:
                        prompt = "Create a JSON with a title and content for a dark psychology TikTok idea about dark psychology, and such encounters. Include examples to make viewers reflect.(Answer only with the json)";
                        tags = new string[] { "#manipulation", "#darkpsychology" };

                        break;
                }

                var message = await _openAIService.AskQuestionAsync(prompt);
                int index1 = message.IndexOf('{');
                int index2 = message.LastIndexOf('}') + 1;
                message = message.Substring(index1, index2 - index1);
                var story = JsonSerializer.Deserialize<StoryDto>(message);

                var result = await _openAIService.GenerateAudioFromTextAsync(story.content, speed: contentInfo.VoiceSpeed,voice:contentInfo.TextToSpeechVoice);
                var words = await _openAIService.TranscribeMp3Async(result);


                _ffmpegService.CreateVideoWithSubtitles(
                    Path.Combine(mediaResourcesPath,contentInfo.BackgroundAudio),
                    Path.Combine(mediaResourcesPath, contentInfo.BackgroundVideo),
                    result, words,
                                        Path.Combine(relativePath, "output.mp4"), true,contentInfo.TextToSpeechVolume,contentInfo.BackgroundAudioVolume
                    );

                _logger.LogInformation($"Task ID {task.Id} processed successfully. TTS saved at {relativePath}.");
                var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
                var accessSession = await taskService.GetSocialMediaAccessSession(task.SocialMediaAccessSessionsId);
                await tiktokService.UploadVideoAsync(accessSession.accesstoken, Path.Combine(relativePath, "output.mp4"), story.title,tags);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing task ID {task.Id}.");
            }
        }
    }
}
