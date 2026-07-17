using Prices.WindowsService.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prices.WindowsService.Helpers
{
    public class BaseJobDependencies
    {
        public IDbConnectionFactory DbConnectionFactory { get; }
        public RetailersHelper RetailersHelper { get; }
        public HttpClient HttpClient { get; }

        public BaseJobDependencies(IDbConnectionFactory dbConnectionFactory, RetailersHelper retailersHelper, HttpClient httpClient)
        {
            DbConnectionFactory = dbConnectionFactory;
            RetailersHelper = retailersHelper;
            HttpClient = httpClient;
        }
    }
}
