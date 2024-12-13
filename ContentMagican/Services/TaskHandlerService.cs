using ContentMagican.Database;
using ContentMagican.DTOs;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ContentMagican.Services
{
    public class TaskHandlerService : BackgroundService
    {
        private readonly ILogger<TaskHandlerService> _logger;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly OpenAIService _openAIService;
        private readonly AzureSpeechService _azureSpeechService;
        private readonly FFmpegService _ffmpegService;

        public TaskHandlerService(
            ILogger<TaskHandlerService> logger,
            IServiceScopeFactory serviceScopeFactory,
            OpenAIService openAIService,
            AzureSpeechService azureSpeechService,
            FFmpegService ffmpegService)
        {
            _logger = logger;
            _serviceScopeFactory = serviceScopeFactory;
            _openAIService = openAIService;
            _azureSpeechService = azureSpeechService;
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
                _logger.LogInformation($"Processing task ID {task.Id}...");

                string tempId = Guid.NewGuid().ToString();
                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string relativePath = Path.Combine(baseDirectory, "temp", "temp", tempId); 
                string ttsPath = Path.Combine(relativePath, "tts.mp3");
                Directory.CreateDirectory(relativePath);

                string prompt = @"
                {
                    ""title"": ""Write a creepy first-person story in Reddit style"",
                    ""content"": ""Use a conversational tone, include realistic details, and focus on suspense. Format the response as a JSON object with 'title' and 'content' fields.""
                }";
                var message = await _openAIService.AskQuestionAsync(prompt);
                int index1 = message.IndexOf('{');
                int index2 = message.LastIndexOf('}')+1;
                message = message.Substring(index1, index2-index1);
                var story = JsonSerializer.Deserialize<StoryDto>(message);

                var result = await _azureSpeechService.SynthesizeSpeechAsync(story.content);
                //await File.WriteAllBytesAsync(ttsPath, result.audioData, stoppingToken);
                var words = await _openAIService.TranscribeMp3Async(result.audioData);


                _ffmpegService.CreateVideoWithSubtitles(
                    @"C:\Users\chfzs\source\repos\ContentMagican\ContentMagican\bin\Debug\net8.0\wwwroot\MediaResources\Audios\Creepy.mp3",
                    @"C:\Users\chfzs\source\repos\ContentMagican\ContentMagican\bin\Debug\net8.0\wwwroot\MediaResources\Videos\MinecraftGameplay.mp4",
                    result.audioData,words,
                    
                    //@"C:\Users\chfzs\source\repos\ContentMagican\ContentMagican\bin\Debug\net8.0\wwwroot\MediaResources\Fonts\Steelfish Outline.otf",
                    Path.Combine(relativePath,"output.mp4")
                    );
                


                _logger.LogInformation($"Task ID {task.Id} processed successfully. TTS saved at {relativePath}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing task ID {task.Id}.");
            }
        }
    }
}
