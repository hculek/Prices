using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;

namespace Prices.WindowsService.CSV_Jobs
{
    public class Base<T> : BackgroundService
    {
        private readonly string _jobName;
        private readonly int _sleepMinutes;
        private readonly int _sleepMinutesFail;
        private readonly ILogger<T> _logger;
        protected Base(ILogger<T> Logger, string JobName, int SleepMinutes, int SleepMinutesFail = 1440)
        {
            _jobName = JobName;
            _sleepMinutes = SleepMinutes * 60000;
            _sleepMinutesFail = SleepMinutesFail * 60000;
            _logger = Logger;
        }
        public virtual async Task Work() 
        { }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation($"CSV job {_jobName} started.");
                    await Work();
                    _logger.LogInformation($"CSV job {_jobName} completed. Sleeping until {DateTime.Now.AddMinutes(_sleepMinutes)}.");
                    await Task.Delay(_sleepMinutes, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"CSV job {_jobName} failed.");
                    await Task.Delay(_sleepMinutesFail, stoppingToken);
                }
            }
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            return base.StartAsync(cancellationToken);
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            return base.StopAsync(cancellationToken);
        }
    }
}
