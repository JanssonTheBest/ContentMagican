using ContentMagican.Database;
using ContentMagican.DTOs;
using Microsoft.AspNetCore.Http.HttpResults;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
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

        HttpClient httpClient = new HttpClient();

#if DEBUG
        string uri = "https://localhost:7088/Account/RetrieveDueContentCreation";
#else
        string uri = "https://www.conjurecontent.com/Account/RetrieveDueContentCreation";
#endif

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

                    //using var scope = _serviceScopeFactory.CreateScope();
                    //var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();



                    //var tasks = await taskService.GetAllActiveTasks();


                    var result = await httpClient.PostAsync(uri, new StringContent(JsonSerializer.Serialize(new Dictionary<string, string>()
                    {
                        { "key","jsNm7x9L#c2x43ezvrtfgsyuhsydehvjndsjhgxycgdshj24343243rt43t4" }
                    }), Encoding.UTF8, "application/json"));
                    var stringResult = await result.Content.ReadAsStringAsync();

                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var contentCreations = JsonSerializer.Deserialize<ContentCreationDto[]>(stringResult, options);



                    if (contentCreations.Select(a => a._Task).Count() == 0)
                    {
                        _logger.LogInformation("No tasks found. Waiting before retrying...");
                        await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
                        continue;
                    }

                    var processingTasks = contentCreations.Select(contentCreation => ProcessTaskAsync(contentCreation, stoppingToken));
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

                await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
            }
        }

        private async Task ProcessTaskAsync(ContentCreationDto contentCreation, CancellationToken stoppingToken)
        {
            for (int i = 0; i < contentCreation.VideoAutomation.Interval; i++)
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var tiktokService = scope.ServiceProvider.GetRequiredService<TiktokService>();

                    _logger.LogInformation($"Processing task ID {contentCreation._Task.Id}...");
                    var contentInfo = JsonSerializer.Deserialize<ContentInfo>(contentCreation.VideoAutomation.FFmpegString);

                    string tempId = Guid.NewGuid().ToString();
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    string relativePath = Path.Combine(baseDirectory, "temp", "temp", tempId);
                    string ttsPath = Path.Combine(relativePath, "tts.mp3");
                    Directory.CreateDirectory(relativePath);
                    string mediaResourcesPath = Path.Combine(baseDirectory, "wwwroot\\MediaResources");
                    string prompt = "";
                    string[] tags = Array.Empty<string>();
                    switch ((TaskService.TaskSubTypes)contentCreation._Task.Subtype)
                    {
                        case TaskService.TaskSubTypes.Reddit_Stories:

                            prompt = $"Create a creative and unique first-person story in Reddit style, incorporating {contentInfo.AdditionalInfo}. Ensure it has a distinct voice and approach to avoid similarities with existing stories. (your answer is only alowed to contain title and content formated in json)";
                            tags = new string[] { "#redditstories", "#redditstory" };

                            break;
                        case TaskService.TaskSubTypes.Dark_Psychology:
                            prompt = "Create a JSON object with only the fields \"title\" and \"content\". The \"title\" should be a catchy title for a TikTok video focused on dark psychology. The \"content\" should include the script for the video's voiceover, which explains a dark psychology concept or encounter, provides specific relatable examples to engage viewers, includes reflection prompts to encourage viewers to think about the topic, and offers advice on how to handle similar situations in the future. Ensure the ideas are varied and diverse. Respond only with the JSON.";
                            tags = new string[] { "#manipulation", "#darkpsychology" };

                            break;
                    }

                    var message = await _openAIService.AskQuestionAsync(prompt);
                    int index1 = message.IndexOf('{');
                    int index2 = message.LastIndexOf('}') + 1;
                    message = message.Substring(index1, index2 - index1);
                    var story = JsonSerializer.Deserialize<StoryDto>(message);

                    var result = await _openAIService.GenerateAudioFromTextAsync(story.content, speed: contentInfo.VoiceSpeed, voice: contentInfo.TextToSpeechVoice);
                    var words = await _openAIService.TranscribeMp3Async(result);

                    _ffmpegService.CreateVideoWithSubtitles(
                        Path.Combine(mediaResourcesPath, contentInfo.BackgroundAudio),
                        Path.Combine(mediaResourcesPath, contentInfo.BackgroundVideo),
                        result, words,
                                            Path.Combine(relativePath, "output.mp4"), true, contentInfo.TextToSpeechVolume, contentInfo.BackgroundAudioVolume
                        );

                    _logger.LogInformation($"Task ID {contentCreation._Task.Id} processed successfully. TTS saved at {relativePath}.");
                    var taskService = scope.ServiceProvider.GetRequiredService<TaskService>();
                    var accessSession = await taskService.GetSocialMediaAccessSession(contentCreation._Task.SocialMediaAccessSessionsId);
                    await tiktokService.UploadVideoAsync(accessSession.accesstoken, Path.Combine(relativePath, "output.mp4"), story.title, tags);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing task ID {contentCreation._Task.Id}.");
                }
            }
        }
    }
}
