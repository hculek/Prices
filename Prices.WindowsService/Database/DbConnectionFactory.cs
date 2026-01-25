using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prices.WindowsService.Database
{
    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;

        public DbConnectionFactory(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("PricesDB");      
        }

        public NpgsqlConnection CreateConnection()
        { 
            return new NpgsqlConnection(_connectionString);
        }

    }
}
