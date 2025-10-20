using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos;
using AirlineAncillary.Domain.Entities;

using AirlineAncillary.Domain.Models;

using AirlineAncillary.Infrastructure.Repositories;
using AirlineAncillary.Domain.Models;
using AirlineAncillary.Infrastructure.CosmosDB;

using System.Net;

using AirlineAncillary.Infrastructure.Repositories;
using Microsoft.Extensions.Logging;

namespace AirlineAncillary.Infrastructure.Implementations

{
    using System.Net;
    using Microsoft.Azure.Cosmos;

    /// <summary>

    /// CosmosDB repository implementation for Offer operations

    /// </summary>

    public class OfferRepository : IOfferRepositorynamespace AirlineAncillary.Infrastructure.Implementationsnamespace AirlineAncillary.Infrastructure.Repositories

    {

        private readonly Container _container;{{

        private const string ContainerName = "offers";

        /// <summary>    /// <summary>

        public OfferRepository(CosmosClient cosmosClient, string databaseName)

        {    /// CosmosDB repository implementation for Offer operations    /// Repository interface for Offer operations

            _container = cosmosClient.GetContainer(databaseName, ContainerName);

        }    /// </summary>    /// </summary>



        public async Task<Offer> CreateOfferAsync(Offer offer)    public class OfferRepository : IOfferRepository    public interface IOfferRepository

        {

            offer.SetTTL(); // Set 40-minute TTL    {    {

            var response = await _container.CreateItemAsync(offer, new PartitionKey(offer.FlightId));

            return response.Resource;        private readonly Container _container; Task<Offer?> GetByIdAsync(string id, string flightId);

        }

        private const string ContainerName = "offers"; Task<Offer> CreateAsync(Offer offer);

        public async Task<Offer?> GetOfferByIdAsync(string id)

        {
            Task<Offer> UpdateAsync(Offer offer);

            try

            {        public OfferRepository(CosmosClient cosmosClient, string databaseName)        Task DeleteAsync(string id, string flightId);

        var response = await _container.ReadItemAsync<Offer>(id, new PartitionKey(id));

                return response.Resource;        {        Task<IEnumerable<Offer>> GetByFlightIdAsync(string flightId);

    }

            catch (CosmosException ex) when(ex.StatusCode == HttpStatusCode.NotFound)            _container = cosmosClient.GetContainer(databaseName, ContainerName);        Task<IEnumerable<Offer>> GetByPassengerIdAsync(string passengerId);

            {

                return null;        }
Task<IEnumerable<Offer>> GetActiveOffersAsync(string flightId);

            }

        }        Task<IEnumerable<Offer>> SearchOffersAsync(OfferSearchCriteria criteria);



public async Task<IEnumerable<Offer>> GetOffersByFlightIdAsync(string flightId)        public async Task<Offer> CreateOfferAsync(Offer offer)        Task<bool> ExpireOfferAsync(string id, string flightId);

{

    var query = new QueryDefinition("SELECT * FROM c WHERE c.flightId = @flightId AND c.status = @status")        {        Task<int> CleanupExpiredOffersAsync();

                .WithParameter("@flightId", flightId)

                .WithParameter("@status", OfferStatus.Active.ToString());            offer.SetTTL(); // Set 40-minute TTL    }



            var iterator = _container.GetItemQueryIterator<Offer>(query);            var response = await _container.CreateItemAsync(offer, new PartitionKey(offer.FlightId));

            var offers = new List<Offer>();

            return response.Resource;    /// <summary>

    while (iterator.HasMoreResults)

    { }    /// Repository interface for Order operations

    var response = await iterator.ReadNextAsync();

    offers.AddRange(response);    /// </summary>

}

public async Task<Offer?> GetOfferByIdAsync(string id)    public interface IOrderRepository

            return offers.Where(o => !o.IsExpired);

        }        {
    {



        public async Task<IEnumerable<Offer>> GetActiveOffersAsync()            try        Task<Order?> GetByIdAsync(string id, string customerId);

{

    var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")            {        Task<Order> CreateAsync(Order order);

                .WithParameter("@status", OfferStatus.Active.ToString());

                var response = await _container.ReadItemAsync<Offer>(id, new PartitionKey(id));        Task<Order> UpdateAsync(Order order);

            var iterator = _container.GetItemQueryIterator<Offer>(query);

            var offers = new List<Offer>();                return response.Resource; Task DeleteAsync(string id, string customerId);



    while (iterator.HasMoreResults)            }
Task<IEnumerable<Order>> GetByCustomerIdAsync(string customerId);

{

    var response = await iterator.ReadNextAsync();            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)        Task<IEnumerable<Order>> GetByFlightIdAsync(string flightId);

offers.AddRange(response);

}
{
    Task<IEnumerable<Order>> GetByOfferIdAsync(string offerId);



    return offers.Where(o => !o.IsExpired); return null; Task<IEnumerable<Order>> SearchOrdersAsync(OrderSearchCriteria criteria);

}

            }        Task<bool> UpdateOrderStatusAsync(string id, string customerId, OrderStatus status, string? reason = null);

public async Task<Offer> UpdateOfferAsync(Offer offer)

{ }    }

            var response = await _container.UpsertItemAsync(offer, new PartitionKey(offer.FlightId));

return response.Resource;

        }

        public async Task<IEnumerable<Offer>> GetOffersByFlightIdAsync(string flightId)    /// <summary>

        public async Task DeleteOfferAsync(string id)

{
    {    /// Implementation of Offer repository

        await _container.DeleteItemAsync<Offer>(id, new PartitionKey(id));

    }
    var query = new QueryDefinition("SELECT * FROM c WHERE c.flightId = @flightId AND c.status = @status")    /// </summary>



        public async Task<bool> OfferExistsAsync(string id)                .WithParameter("@flightId", flightId)    public class OfferRepository : IOfferRepository

{

            try                .WithParameter("@status", OfferStatus.Active.ToString());    {

            {

                await _container.ReadItemAsync<Offer>(id, new PartitionKey(id));        private readonly ICosmosDbRepository<Offer> _repository;

                return true;

            }
var iterator = _container.GetItemQueryIterator<Offer>(query); private readonly ILogger<OfferRepository> _logger;

            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)

            {            var offers = new List<Offer>();

return false;

            }        public OfferRepository(ICosmosDbClientFactory clientFactory, ILogger<OfferRepository> logger)

        }

    }            while (iterator.HasMoreResults)
{



    /// <summary>            {            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// CosmosDB repository implementation for Order operations

    /// </summary>                var response = await iterator.ReadNextAsync();            _repository = new CosmosDbRepository<Offer>(clientFactory.GetOffersContainer(),

    public class OrderRepository : IOrderRepository

{
    offers.AddRange(response);                new Logger<CosmosDbRepository<Offer>>(new LoggerFactory()));

        private readonly Container _container;

    private const string ContainerName = "orders";
}        }



        public OrderRepository(CosmosClient cosmosClient, string databaseName)

{

    _container = cosmosClient.GetContainer(databaseName, ContainerName); return offers.Where(o => !o.IsExpired);        public async Task<Offer?> GetByIdAsync(string id, string flightId)

        }

        }        {

        public async Task<Order> CreateOrderAsync(Order order)

{
    try

            var response = await _container.CreateItemAsync(order, new PartitionKey(order.CustomerId));

    return response.Resource;        public async Task<IEnumerable<Offer>> GetActiveOffersAsync()
{

}

{
    _logger.LogDebug("Getting offer {OfferId} for flight {FlightId}", id, flightId);

        public async Task<Order?> GetOrderByIdAsync(string id)

{
    var query = new QueryDefinition("SELECT * FROM c WHERE c.status = @status")                return await _repository.GetByIdAsync(id, flightId);

    try

    {                .WithParameter("@status", OfferStatus.Active.ToString()); }

                var response = await _container.ReadItemAsync<Order>(id, new PartitionKey(id));

    return response.Resource;            catch (Exception ex)

            }

            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)            var iterator = _container.GetItemQueryIterator<Offer>(query);
{

    {

        return null; var offers = new List<Offer>(); _logger.LogError(ex, "Error retrieving offer {OfferId} for flight {FlightId}", id, flightId);

    }

}
throw;



public async Task<IEnumerable<Order>> GetOrdersByCustomerIdAsync(string customerId)            while (iterator.HasMoreResults)            }

        {

    var query = new QueryDefinition("SELECT * FROM c WHERE c.customerId = @customerId") { }

        .WithParameter("@customerId", customerId);

    var response = await iterator.ReadNextAsync();

    var iterator = _container.GetItemQueryIterator<Order>(query);

    var orders = new List<Order>(); offers.AddRange(response);        public async Task<Offer> CreateAsync(Offer offer)



            while (iterator.HasMoreResults)            }        {

    {

        var response = await iterator.ReadNextAsync(); try

                orders.AddRange(response);

        }            return offers.Where(o => !o.IsExpired);
        {



            return orders;
        }
        _logger.LogDebug("Creating offer {OfferId} for flight {FlightId}", offer.Id, offer.FlightId);

    }



        public async Task<IEnumerable<Order>> GetOrdersByFlightIdAsync(string flightId)

{        public async Task<Offer> UpdateOfferAsync(Offer offer)                // Set creation timestamp and calculate valid until

            var query = new QueryDefinition("SELECT * FROM c WHERE c.flightId = @flightId")

                .WithParameter("@flightId", flightId);
{
    offer.CreatedAt = DateTime.UtcNow;



    var iterator = _container.GetItemQueryIterator<Order>(query); var response = await _container.UpsertItemAsync(offer, new PartitionKey(offer.FlightId)); offer.ValidUntil = offer.CreatedAt.AddSeconds(offer.Ttl);

    var orders = new List<Order>();

    return response.Resource;

    while (iterator.HasMoreResults)

    { }
    var result = await _repository.CreateAsync(offer);

    var response = await iterator.ReadNextAsync();

    orders.AddRange(response);

}

public async Task DeleteOfferAsync(string id)                _logger.LogInformation("Successfully created offer {OfferId} for flight {FlightId}",

            return orders;

        }        {
    result.Id, result.FlightId);



        public async Task<Order> UpdateOrderAsync(Order order)            await _container.DeleteItemAsync<Offer>(id, new PartitionKey(id));

{

    var response = await _container.UpsertItemAsync(order, new PartitionKey(order.CustomerId));
}
return result;

return response.Resource;

        }            }



        public async Task DeleteOrderAsync(string id)        public async Task<bool> OfferExistsAsync(string id)            catch (Exception ex)

        {

            await _container.DeleteItemAsync<Order>(id, new PartitionKey(id));
{
    {

    }

    try                _logger.LogError(ex, "Error creating offer for flight {FlightId}", offer.FlightId);

        public async Task<bool> OrderExistsAsync(string id)

{
    {
        throw;

        try

        { await _container.ReadItemAsync<Offer>(id, new PartitionKey(id)); }

                await _container.ReadItemAsync<Order>(id, new PartitionKey(id));

        return true; return true;
    }

}

            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)            }

            {

    return false;            catch (CosmosException ex) when(ex.StatusCode == HttpStatusCode.NotFound)        public async Task<Offer> UpdateAsync(Offer offer)

            }

        }            {
    {

    }

}
return false; try

            }            {

}
_logger.LogDebug("Updating offer {OfferId}", offer.Id);

    }

                // Update the modification timestamp

    /// <summary>                offer.ValidUntil = DateTime.UtcNow.AddSeconds(offer.Ttl);

    /// CosmosDB repository implementation for Order operations

    /// </summary>                var result = await _repository.UpdateAsync(offer);

    public class OrderRepository : IOrderRepository

{
    _logger.LogInformation("Successfully updated offer {OfferId}", offer.Id);

        private readonly Container _container;

    private const string ContainerName = "orders";                return result;

            }

public OrderRepository(CosmosClient cosmosClient, string databaseName)            catch (Exception ex)

        {            {

            _container = cosmosClient.GetContainer(databaseName, ContainerName); _logger.LogError(ex, "Error updating offer {OfferId}", offer.Id);

        }                throw;

            }

        public async Task<Order> CreateOrderAsync(Order order)        }

        {

    var response = await _container.CreateItemAsync(order, new PartitionKey(order.CustomerId));        public async Task DeleteAsync(string id, string flightId)

            return response.Resource;
{

}
try

{

        public async Task<Order?> GetOrderByIdAsync(string id)                _logger.LogDebug("Deleting offer {OfferId} for flight {FlightId}", id, flightId);

{

    try                await _repository.DeleteAsync(id, flightId);

    {

        var response = await _container.ReadItemAsync<Order>(id, new PartitionKey(id)); _logger.LogInformation("Successfully deleted offer {OfferId}", id);

        return response.Resource;
    }

    }
    catch (Exception ex)

            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
    {

        {
            _logger.LogError(ex, "Error deleting offer {OfferId} for flight {FlightId}", id, flightId);

            return null; throw;

        }
    }

}        }



        public async Task<IEnumerable<Order>> GetOrdersByCustomerIdAsync(string customerId)        public async Task<IEnumerable<Offer>> GetByFlightIdAsync(string flightId)

{
    {

        var query = new QueryDefinition("SELECT * FROM c WHERE c.customerId = @customerId")            try

                .WithParameter("@customerId", customerId);
        {

            _logger.LogDebug("Getting offers for flight {FlightId}", flightId);

            var iterator = _container.GetItemQueryIterator<Order>(query);

            var orders = new List<Order>(); var query = "SELECT * FROM c WHERE c.flightId = @flightId AND c.status = @status";

            var parameters = new { flightId, status = OfferStatus.Active.ToString() };

            while (iterator.HasMoreResults)

            {
                var results = await _repository.QueryAsync(query, parameters);

                var response = await iterator.ReadNextAsync();

                orders.AddRange(response); _logger.LogDebug("Retrieved {Count} offers for flight {FlightId}",

            }
            results.Count(), flightId);



            return orders; return results;

        }
        }

        catch (Exception ex)

        public async Task<IEnumerable<Order>> GetOrdersByFlightIdAsync(string flightId)
{

    {
        _logger.LogError(ex, "Error retrieving offers for flight {FlightId}", flightId);

        var query = new QueryDefinition("SELECT * FROM c WHERE c.flightId = @flightId")                throw;

                .WithParameter("@flightId", flightId);
    }

}

var iterator = _container.GetItemQueryIterator<Order>(query);

var orders = new List<Order>(); public async Task<IEnumerable<Offer>> GetByPassengerIdAsync(string passengerId)

{

    while (iterator.HasMoreResults) try

        {
            {

                var response = await iterator.ReadNextAsync(); _logger.LogDebug("Getting offers for passenger {PassengerId}", passengerId);

                orders.AddRange(response);

            }
            var query = "SELECT * FROM c WHERE c.passengerId = @passengerId AND c.status = @status";

            var parameters = new { passengerId, status = OfferStatus.Active.ToString() };

            return orders;

        }                var results = await _repository.QueryAsync(query, parameters);



        public async Task<Order> UpdateOrderAsync(Order order)                _logger.LogDebug("Retrieved {Count} offers for passenger {PassengerId}",

        {
    results.Count(), passengerId);

    var response = await _container.UpsertItemAsync(order, new PartitionKey(order.CustomerId));

    return response.Resource; return results;

}            }

            catch (Exception ex)

        public async Task DeleteOrderAsync(string id)
{

    {
        _logger.LogError(ex, "Error retrieving offers for passenger {PassengerId}", passengerId);

        await _container.DeleteItemAsync<Order>(id, new PartitionKey(id)); throw;

    }
}

        }

        public async Task<bool> OrderExistsAsync(string id)

{        public async Task<IEnumerable<Offer>> GetActiveOffersAsync(string flightId)

            try
{

    {
        try

                await _container.ReadItemAsync<Order>(id, new PartitionKey(id));
        {

            return true; _logger.LogDebug("Getting active offers for flight {FlightId}", flightId);

        }

            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)                var query = @"SELECT * FROM c 

            {                             WHERE c.flightId = @flightId 

                return false;                             AND c.status = @status 

            }                             AND c.validUntil > GetCurrentDateTime()";

        }
        var parameters = new { flightId, status = OfferStatus.Active.ToString() };

    }

}                var results = await _repository.QueryAsync(query, parameters);

_logger.LogDebug("Retrieved {Count} active offers for flight {FlightId}",
    results.Count(), flightId);

return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active offers for flight {FlightId}", flightId);
throw;
            }
        }

        public async Task<IEnumerable<Offer>> SearchOffersAsync(OfferSearchCriteria criteria)
{
    try
    {
        _logger.LogDebug("Searching offers with criteria: {@Criteria}", criteria);

        var queryBuilder = new List<string> { "SELECT * FROM c WHERE 1=1" };
        var parameters = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(criteria.FlightId))
        {
            queryBuilder.Add("AND c.flightId = @flightId");
            parameters["flightId"] = criteria.FlightId;
        }

        if (!string.IsNullOrEmpty(criteria.PassengerId))
        {
            queryBuilder.Add("AND c.passengerId = @passengerId");
            parameters["passengerId"] = criteria.PassengerId;
        }

        if (!string.IsNullOrEmpty(criteria.AncillaryType))
        {
            queryBuilder.Add("AND c.ancillaryType = @ancillaryType");
            parameters["ancillaryType"] = criteria.AncillaryType;
        }

        if (criteria.Status.HasValue)
        {
            queryBuilder.Add("AND c.status = @status");
            parameters["status"] = criteria.Status.Value.ToString();
        }

        if (criteria.MinPrice.HasValue)
        {
            queryBuilder.Add("AND c.price.amount >= @minPrice");
            parameters["minPrice"] = criteria.MinPrice.Value;
        }

        if (criteria.MaxPrice.HasValue)
        {
            queryBuilder.Add("AND c.price.amount <= @maxPrice");
            parameters["maxPrice"] = criteria.MaxPrice.Value;
        }

        if (criteria.CreatedAfter.HasValue)
        {
            queryBuilder.Add("AND c.createdAt >= @createdAfter");
            parameters["createdAfter"] = criteria.CreatedAfter.Value;
        }

        if (criteria.CreatedBefore.HasValue)
        {
            queryBuilder.Add("AND c.createdAt <= @createdBefore");
            parameters["createdBefore"] = criteria.CreatedBefore.Value;
        }

        // Add sorting
        if (!string.IsNullOrEmpty(criteria.SortBy))
        {
            var sortDirection = criteria.SortDescending ? "DESC" : "ASC";
            queryBuilder.Add($"ORDER BY c.{criteria.SortBy} {sortDirection}");
        }

        var query = string.Join(" ", queryBuilder);
        var results = await _repository.QueryAsync(query, parameters);

        _logger.LogDebug("Search completed, found {Count} offers", results.Count());

        return results;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error searching offers with criteria: {@Criteria}", criteria);
        throw;
    }
}

public async Task<bool> ExpireOfferAsync(string id, string flightId)
{
    try
    {
        _logger.LogDebug("Expiring offer {OfferId}", id);

        var offer = await GetByIdAsync(id, flightId);
        if (offer == null)
        {
            _logger.LogWarning("Offer {OfferId} not found for expiration", id);
            return false;
        }

        offer.Status = OfferStatus.Expired;
        await UpdateAsync(offer);

        _logger.LogInformation("Successfully expired offer {OfferId}", id);

        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error expiring offer {OfferId}", id);
        throw;
    }
}

public async Task<int> CleanupExpiredOffersAsync()
{
    try
    {
        _logger.LogDebug("Starting cleanup of expired offers");

        var query = @"SELECT * FROM c 
                             WHERE c.status = @activeStatus 
                             AND c.validUntil < GetCurrentDateTime()";
        var parameters = new { activeStatus = OfferStatus.Active.ToString() };

        var expiredOffers = await _repository.QueryAsync(query, parameters);
        var count = 0;

        foreach (var offer in expiredOffers)
        {
            try
            {
                offer.Status = OfferStatus.Expired;
                await UpdateAsync(offer);
                count++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to expire offer {OfferId}", offer.Id);
            }
        }

        _logger.LogInformation("Cleanup completed, expired {Count} offers", count);

        return count;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error during offer cleanup");
        throw;
    }
}
    }

    /// <summary>
    /// Implementation of Order repository
    /// </summary>
    public class OrderRepository : IOrderRepository
{
    private readonly ICosmosDbRepository<Order> _repository;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(ICosmosDbClientFactory clientFactory, ILogger<OrderRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _repository = new CosmosDbRepository<Order>(clientFactory.GetOrdersContainer(),
            new Logger<CosmosDbRepository<Order>>(new LoggerFactory()));
    }

    public async Task<Order?> GetByIdAsync(string id, string customerId)
    {
        try
        {
            _logger.LogDebug("Getting order {OrderId} for customer {CustomerId}", id, customerId);
            return await _repository.GetByIdAsync(id, customerId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order {OrderId} for customer {CustomerId}", id, customerId);
            throw;
        }
    }

    public async Task<Order> CreateAsync(Order order)
    {
        try
        {
            _logger.LogDebug("Creating order {OrderId} for customer {CustomerId}", order.Id, order.CustomerId);

            // Set timestamps
            order.CreatedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            // Generate confirmation number if not provided
            if (string.IsNullOrEmpty(order.ConfirmationNumber))
            {
                order.ConfirmationNumber = GenerateConfirmationNumber();
            }

            var result = await _repository.CreateAsync(order);

            _logger.LogInformation("Successfully created order {OrderId} with confirmation {ConfirmationNumber}",
                result.Id, result.ConfirmationNumber);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order for customer {CustomerId}", order.CustomerId);
            throw;
        }
    }

    public async Task<Order> UpdateAsync(Order order)
    {
        try
        {
            _logger.LogDebug("Updating order {OrderId}", order.Id);

            // Update timestamp
            order.UpdatedAt = DateTime.UtcNow;

            // Set completion timestamp if status is completed
            if (order.Status == OrderStatus.Fulfilled && !order.CompletedAt.HasValue)
            {
                order.CompletedAt = DateTime.UtcNow;
            }

            // Set cancellation timestamp if status is cancelled
            if (order.Status == OrderStatus.Cancelled && !order.CancelledAt.HasValue)
            {
                order.CancelledAt = DateTime.UtcNow;
            }

            var result = await _repository.UpdateAsync(order);

            _logger.LogInformation("Successfully updated order {OrderId}", order.Id);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId}", order.Id);
            throw;
        }
    }

    public async Task DeleteAsync(string id, string customerId)
    {
        try
        {
            _logger.LogDebug("Deleting order {OrderId} for customer {CustomerId}", id, customerId);

            await _repository.DeleteAsync(id, customerId);

            _logger.LogInformation("Successfully deleted order {OrderId}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting order {OrderId} for customer {CustomerId}", id, customerId);
            throw;
        }
    }

    public async Task<IEnumerable<Order>> GetByCustomerIdAsync(string customerId)
    {
        try
        {
            _logger.LogDebug("Getting orders for customer {CustomerId}", customerId);

            var results = await _repository.GetAllAsync(customerId);

            _logger.LogDebug("Retrieved {Count} orders for customer {CustomerId}",
                results.Count(), customerId);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for customer {CustomerId}", customerId);
            throw;
        }
    }

    public async Task<IEnumerable<Order>> GetByFlightIdAsync(string flightId)
    {
        try
        {
            _logger.LogDebug("Getting orders for flight {FlightId}", flightId);

            var query = "SELECT * FROM c WHERE c.flightId = @flightId";
            var parameters = new { flightId };

            var results = await _repository.QueryAsync(query, parameters);

            _logger.LogDebug("Retrieved {Count} orders for flight {FlightId}",
                results.Count(), flightId);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for flight {FlightId}", flightId);
            throw;
        }
    }

    public async Task<IEnumerable<Order>> GetByOfferIdAsync(string offerId)
    {
        try
        {
            _logger.LogDebug("Getting orders for offer {OfferId}", offerId);

            var query = "SELECT * FROM c WHERE c.offerId = @offerId";
            var parameters = new { offerId };

            var results = await _repository.QueryAsync(query, parameters);

            _logger.LogDebug("Retrieved {Count} orders for offer {OfferId}",
                results.Count(), offerId);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for offer {OfferId}", offerId);
            throw;
        }
    }

    public async Task<IEnumerable<Order>> SearchOrdersAsync(OrderSearchCriteria criteria)
    {
        try
        {
            _logger.LogDebug("Searching orders with criteria: {@Criteria}", criteria);

            var queryBuilder = new List<string> { "SELECT * FROM c WHERE 1=1" };
            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(criteria.CustomerId))
            {
                queryBuilder.Add("AND c.customerId = @customerId");
                parameters["customerId"] = criteria.CustomerId;
            }

            if (!string.IsNullOrEmpty(criteria.FlightId))
            {
                queryBuilder.Add("AND c.flightId = @flightId");
                parameters["flightId"] = criteria.FlightId;
            }

            if (criteria.Status.HasValue)
            {
                queryBuilder.Add("AND c.status = @status");
                parameters["status"] = criteria.Status.Value.ToString();
            }

            if (criteria.MinAmount.HasValue)
            {
                queryBuilder.Add("AND c.totalAmount.amount >= @minAmount");
                parameters["minAmount"] = criteria.MinAmount.Value;
            }

            if (criteria.MaxAmount.HasValue)
            {
                queryBuilder.Add("AND c.totalAmount.amount <= @maxAmount");
                parameters["maxAmount"] = criteria.MaxAmount.Value;
            }

            if (criteria.CreatedAfter.HasValue)
            {
                queryBuilder.Add("AND c.createdAt >= @createdAfter");
                parameters["createdAfter"] = criteria.CreatedAfter.Value;
            }

            if (criteria.CreatedBefore.HasValue)
            {
                queryBuilder.Add("AND c.createdAt <= @createdBefore");
                parameters["createdBefore"] = criteria.CreatedBefore.Value;
            }

            // Add sorting
            if (!string.IsNullOrEmpty(criteria.SortBy))
            {
                var sortDirection = criteria.SortDescending ? "DESC" : "ASC";
                queryBuilder.Add($"ORDER BY c.{criteria.SortBy} {sortDirection}");
            }

            var query = string.Join(" ", queryBuilder);
            var results = await _repository.QueryAsync(query, parameters);

            _logger.LogDebug("Search completed, found {Count} orders", results.Count());

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching orders with criteria: {@Criteria}", criteria);
            throw;
        }
    }

    public async Task<bool> UpdateOrderStatusAsync(string id, string customerId, OrderStatus status, string? reason = null)
    {
        try
        {
            _logger.LogDebug("Updating order {OrderId} status to {Status}", id, status);

            var order = await GetByIdAsync(id, customerId);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for status update", id);
                return false;
            }

            order.Status = status;
            if (!string.IsNullOrEmpty(reason))
            {
                order.Notes = string.IsNullOrEmpty(order.Notes)
                    ? reason
                    : $"{order.Notes}; {reason}";
            }

            await UpdateAsync(order);

            _logger.LogInformation("Successfully updated order {OrderId} status to {Status}", id, status);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId} status", id);
            throw;
        }
    }

    private static string GenerateConfirmationNumber()
    {
        // Generate a unique confirmation number
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var random = new Random().Next(1000, 9999).ToString();
        return $"AAM{timestamp[^6..]}{random}"; // AAM + last 6 digits of timestamp + random 4 digits
    }
}

/// <summary>
/// Search criteria for offers
/// </summary>
public class OfferSearchCriteria
{
    public string? FlightId { get; set; }
    public string? PassengerId { get; set; }
    public string? AncillaryType { get; set; }
    public OfferStatus? Status { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public bool SortDescending { get; set; } = true;
}

/// <summary>
/// Search criteria for orders
/// </summary>
public class OrderSearchCriteria
{
    public string? CustomerId { get; set; }
    public string? FlightId { get; set; }
    public OrderStatus? Status { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public string? SortBy { get; set; } = "createdAt";
    public bool SortDescending { get; set; } = true;
}
}