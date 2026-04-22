using DocumentFormat.OpenXml.Drawing.Diagrams;
using HtmlAgilityPack;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Prices.WindowsService.Database;
using Prices.WindowsService.Helpers;
using Prices.WindowsService.POCO;
using System.IO.Compression;
using System.Text;

namespace Prices.WindowsService.CSV_Jobs
{
    public class Base<T> : BackgroundService
    {
        private readonly string _jobName;
        private int _sleepMinutes;
        private int _sleepMinutesFail;
        private readonly ILogger<T> _logger;
        private readonly HtmlWeb _HtmlAgilityWeb;
        private readonly IDbConnectionFactory _dbConnectionFactory;
        private readonly RetailersHelper _retailersHelper;
        public Base(ILogger<T> Logger, IDbConnectionFactory DbConnFactory, RetailersHelper RetailersHelper, 
            string JobName, int SleepMinutes = 15, int? SleepMinutesFail = 1440)
        {
            _jobName = JobName;
            _sleepMinutes = SleepMinutes;
            _sleepMinutesFail = SleepMinutesFail.Value;
            _logger = Logger;
            _HtmlAgilityWeb = new HtmlWeb();
            _dbConnectionFactory = DbConnFactory;
            _retailersHelper = RetailersHelper;
        }
        public virtual async Task Work() 
        { }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation($"{_jobName} started.");
                    await Work();
                    _logger.LogInformation($"{_jobName} completed. Sleeping until {DateTime.Now.AddMinutes(_sleepMinutes)}.");
                    await Task.Delay(TimeSpan.FromMinutes(_sleepMinutes), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"{_jobName} failed.");
                    await Task.Delay(TimeSpan.FromMinutes(_sleepMinutesFail), stoppingToken);
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

        public async Task DownloadCSVAsync(string downloadUrl, int retailerId, int storeId, string saveLocation)
        {
            using (HttpClient hc = new HttpClient())
            {
                byte[] csv = await hc.GetByteArrayAsync(downloadUrl);
                await File.WriteAllBytesAsync(saveLocation + @$"\{retailerId}_{storeId}.csv", csv);
            }
        }

        /// <summary>
        /// using (ZipArchive zip = await DownloadZipAsync(url))
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<ZipArchive> DownloadZipAsync(string url) 
        {
            HttpClient hc = new HttpClient();

            var stream = await hc.GetStreamAsync(url);

            return new ZipArchive(stream, ZipArchiveMode.Read);
        }

        public async Task SaveCsvFromZip(ZipArchiveEntry zip, int retailerId, int storeId, string saveLocation) 
        {
            string savePath = Path.Combine(saveLocation, $"{retailerId}_{storeId}.csv");
            
            using (Stream zipStream = zip.Open())
            {
                using (FileStream fs = File.Create(savePath))
                {
                    await zipStream.CopyToAsync(fs);
                }
            }
        }

        public async Task<RetailerDataPOCO> GetRetailerBasicDataAsync(RetailersEnum retailer)
        {
            return await _retailersHelper.GetRetailerBasicData(retailer);
        }

        public async Task<List<RetailerBusinessUnitPOCO>> GetStoresAsync(int retailerID)
        {
            List<RetailerBusinessUnitPOCO> result = new List<RetailerBusinessUnitPOCO>();

            try
            {
                using (NpgsqlConnection conn = _dbConnectionFactory.CreateConnection())
                {
                    string query = @"SELECT 
                                        u.retailer_id, 
                                        u.unit_id, 
                                        u.lookup, 
                                        u.filename
                                    from crm.retailer_business_unit_data u
                                    where u.is_active = true
                                    and u.retailer_id = @retailerID";

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


        public async Task InsertImportLogs(List<ImportLog> importLogs) 
        {
            try
            {
                if (importLogs.Any())
                {
                    await using (NpgsqlConnection conn = _dbConnectionFactory.CreateConnection())
                    {
                        await conn.OpenAsync();

                        await using (NpgsqlTransaction transaction = await conn.BeginTransactionAsync())
                        {
                            await using (NpgsqlCommand cmd = new NpgsqlCommand(
                                @"INSERT INTO data_import.import_logs (retailer_id, unit_id, last_update_date)
                              VALUES (@retailerId, @unitId, now())", conn, transaction))
                            {
                                cmd.Parameters.Add("@retailerId", NpgsqlTypes.NpgsqlDbType.Integer);
                                cmd.Parameters.Add("@unitId", NpgsqlTypes.NpgsqlDbType.Integer);

                                await cmd.PrepareAsync();

                                foreach (var log in importLogs)
                                {
                                    cmd.Parameters["@retailerId"].Value = log.retailerID;
                                    cmd.Parameters["@unitId"].Value = log.unitID;
                                    await cmd.ExecuteNonQueryAsync();
                                }

                                await transaction.CommitAsync();
                            }
                        }
                    }
                }  
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
        }

        public void SetSleepMinutes(int minutes)
        {
            _sleepMinutes = minutes;
        }

        public void SetSleepMinutesFail(int minutes)
        {
            _sleepMinutesFail = minutes;
        }
    }
}
