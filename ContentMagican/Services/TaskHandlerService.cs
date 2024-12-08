namespace ContentMagican.Services
{
    public class TaskHandlerService : BackgroundService
    {
        private readonly ILogger<TaskHandlerService> _logger;

        public TaskHandlerService(ILogger<TaskHandlerService> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("MyBackgroundService is working.");

                    await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
                }
                catch (Exception ex)
                {
                }
            }

        }
    }
}
