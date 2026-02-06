using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;
using Prices.WindowsService.CSV_Jobs;
using Prices.WindowsService.Database;
using Prices.WindowsService.Helpers;

namespace Prices.WindowsService
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
            .UseWindowsService()
            .ConfigureAppConfiguration(config =>
            {
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                config.AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: true);
                config.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
                config.AddEnvironmentVariables();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Trace);
                logging.AddNLog();
            })
            .ConfigureServices((hostContext, services) =>
            {
                Console.WriteLine($"Environment: {hostContext.HostingEnvironment.EnvironmentName}");

                services.AddWindowsService();

                #region hosted services
                //services.AddHostedService<KonzumJob>();
                //services.AddHostedService<KTCJob>();
                services.AddHostedService<LidlJob>();
                #endregion hosted services

                services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
                services.AddSingleton<RetailersHelper>();
            });
    }
}
