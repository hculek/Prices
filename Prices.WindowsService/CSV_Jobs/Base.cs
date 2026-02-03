using HtmlAgilityPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using Npgsql;
using Prices.WindowsService.Database;
using Prices.WindowsService.POCO;
using System.Net;
using System.Text;

namespace Prices.WindowsService.CSV_Jobs
{
    public class Base<T> : BackgroundService
    {
        private readonly string _jobName;
        private readonly int _sleepMinutes;
        private readonly int _sleepMinutesFail;
        private readonly ILogger<T> _logger;
        private readonly HtmlWeb _HtmlAgilityWeb;
        private readonly IDbConnectionFactory _dbConnectionFactory;
        protected Base(ILogger<T> Logger, IDbConnectionFactory DbConnFactory, string JobName, int SleepMinutes, int SleepMinutesFail = 1440)
        {
            _jobName = JobName;
            _sleepMinutes = SleepMinutes * 60000;
            _sleepMinutesFail = SleepMinutesFail * 60000;
            _logger = Logger;
            _HtmlAgilityWeb = new HtmlWeb();
            _dbConnectionFactory = DbConnFactory;
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

        public async Task<HtmlDocument> GetWebDocAsync(string url, Encoding? encoding = null)
        {
            _HtmlAgilityWeb.OverrideEncoding = encoding ?? Encoding.UTF8;
            return _HtmlAgilityWeb.Load(url);
        }

        public async Task<List<RetailerBusinessUnitPOCO>> GetStoresAsync(int retailerID)
        {
            List<RetailerBusinessUnitPOCO> result = new List<RetailerBusinessUnitPOCO>();

            try
            {
                using (NpgsqlConnection conn = _dbConnectionFactory.CreateConnection())
                {
                    string query = @"SELECT retailer_id, unit_id, lookup, filename 
                                    from crm.retailer_business_unit_data 
                                    where is_active = true
                                    and retailer_id = @retailerID";

                    await conn.OpenAsync();

                    using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@retailerID", retailerID);

                        using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.Add(new RetailerBusinessUnitPOCO
                                {
                                    retailerID = reader.GetInt32(reader.GetOrdinal("retailer_id")),
                                    unitID = reader.GetInt32(reader.GetOrdinal("unit_id")),
                                    lookup = reader.GetValue(reader.GetOrdinal("lookup")) as string,
                                    filename = reader.GetValue(reader.GetOrdinal("filename")) as string
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
           
            return result;
        }

        public async Task DownloadCSVAsync(string downloadUrl, int retailerId, int storeId, string saveLocation)
        {
            using (HttpClient hc = new HttpClient())
            { 
                byte[] csv = await hc.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(saveLocation+@$"\{retailerId}_{storeId}.csv", csv);
            }
        }
    }
}
