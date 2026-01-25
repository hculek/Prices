using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Prices.WindowsService.Database
{
    public interface IDbConnectionFactory
    {
        public NpgsqlConnection CreateConnection();
    }
}
