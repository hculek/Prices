using Npgsql;
using Prices.WindowsService.Database;

namespace Prices.WindowsService.Helpers
{
    public interface IRetailersData
    {
        public List<RetailerDataPOCO> retailers { get; }
    }

    public class RetailerDataPOCO
    {
        public int retailerId { get; set; }
        public string retailerName { get; set; }
        public string csvDirectory { get; set; }
    }

    /// <summary>
    /// alphabetical enum for retailer names
    /// </summary>
    public enum RetailersEnum 
    {
        DM,
        LIDL,
        KAUFLAND,
        KONZUM,
        KTC,
        PLODINE  
    }

    public class RetailersHelper : IRetailersData
    {
        public List<RetailerDataPOCO> retailers { get; }


        private readonly IDbConnectionFactory _dbConnectionFactory;

        public RetailersHelper(IDbConnectionFactory DbConnFactory)
        {
            _dbConnectionFactory = DbConnFactory;
            retailers = new List<RetailerDataPOCO>();

            using (NpgsqlConnection conn = _dbConnectionFactory.CreateConnection())
            {
                conn.Open();

                string query = "SELECT retailer_id, retailer_name, csv_directory FROM crm.retailer_basic_data WHERE is_active = true";

                using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
                {
                    using (NpgsqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            retailers.Add(new RetailerDataPOCO
                            {
                                retailerId = reader.GetInt32(reader.GetOrdinal("retailer_id")),
                                retailerName = reader.GetValue(reader.GetOrdinal("retailer_name")) as string,
                                csvDirectory = reader.GetValue(reader.GetOrdinal("csv_directory")) as string
                            });
                        }
                    }
                }
            }
        }

        public async Task<RetailerDataPOCO> GetRetailerBasicData(RetailersEnum retailer) 
        {
            return retailers.Where(x => x.retailerName.Equals(retailer.ToString())).FirstOrDefault();
        }
    }
}
