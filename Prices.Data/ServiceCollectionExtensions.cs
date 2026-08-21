using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prices.Data.Database;

namespace Prices.Data
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDAL(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();

            return services;
        }
    }
}
