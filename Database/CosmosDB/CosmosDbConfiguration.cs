using Microsoft.Azure.Cosmos;
using AirlineAncillary.Infrastructure.Repositories;
using AirlineAncillary.Infrastructure.Implementations;

namespace AirlineAncillary.Infrastructure.Configuration
{
    /// <summary>
    /// CosmosDB configuration and service registration
    /// </summary>
    public static class CosmosDbConfiguration
    {
        public static IServiceCollection AddCosmosDb(this IServiceCollection services, IConfiguration configuration)
        {
            var cosmosDbSettings = configuration.GetSection("CosmosDb").Get<CosmosDbSettings>()
                                 ?? throw new InvalidOperationException("CosmosDb configuration is missing");

            // Register CosmosClient as singleton
            services.AddSingleton<CosmosClient>(serviceProvider =>
            {
                return new CosmosClient(cosmosDbSettings.ConnectionString, new CosmosClientOptions
                {
                    ApplicationName = "AirlineAncillaryAPI",
                    ConnectionMode = ConnectionMode.Direct,
                    ConsistencyLevel = ConsistencyLevel.Session
                });
            });

            // Register repositories
            services.AddScoped<IOfferRepository>(serviceProvider =>
            {
                var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
                return new OfferRepository(cosmosClient, cosmosDbSettings.DatabaseName);
            });

            services.AddScoped<IOrderRepository>(serviceProvider =>
            {
                var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
                return new OrderRepository(cosmosClient, cosmosDbSettings.DatabaseName);
            });

            return services;
        }

        /// <summary>
        /// Initialize CosmosDB database and containers
        /// </summary>
        public static async Task InitializeCosmosDbAsync(this IServiceProvider serviceProvider)
        {
            var cosmosClient = serviceProvider.GetRequiredService<CosmosClient>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();
            var cosmosDbSettings = configuration.GetSection("CosmosDb").Get<CosmosDbSettings>()!;

            // Create database if it doesn't exist
            var databaseResponse = await cosmosClient.CreateDatabaseIfNotExistsAsync(
                cosmosDbSettings.DatabaseName,
                ThroughputProperties.CreateAutoscaleThroughput(1000));

            var database = databaseResponse.Database;

            // Create offers container with TTL enabled
            await database.CreateContainerIfNotExistsAsync(new ContainerProperties
            {
                Id = "offers",
                PartitionKeyPath = "/flightId",
                DefaultTimeToLive = -1 // Enable TTL, individual items can set their own TTL
            });

            // Create orders container
            await database.CreateContainerIfNotExistsAsync(new ContainerProperties
            {
                Id = "orders",
                PartitionKeyPath = "/customerId"
            });
        }
    }

    /// <summary>
    /// CosmosDB settings configuration
    /// </summary>
    public class CosmosDbSettings
    {
        public string ConnectionString { get; set; } = string.Empty;
        public string DatabaseName { get; set; } = "AirlineAncillaryDB";
    }
}