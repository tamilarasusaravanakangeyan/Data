using AirlineAncillary.Domain.Models;

namespace AirlineAncillary.Infrastructure.Repositories
{
    /// <summary>
    /// Repository interface for Offer operations
    /// </summary>
    public interface IOfferRepository
    {
        Task<Offer> CreateOfferAsync(Offer offer);
        Task<Offer?> GetOfferByIdAsync(string id);
        Task<IEnumerable<Offer>> GetOffersByFlightIdAsync(string flightId);
        Task<IEnumerable<Offer>> GetActiveOffersAsync();
        Task<Offer> UpdateOfferAsync(Offer offer);
        Task DeleteOfferAsync(string id);
        Task<bool> OfferExistsAsync(string id);
    }

    /// <summary>
    /// Repository interface for Order operations
    /// </summary>
    public interface IOrderRepository
    {
        Task<Order> CreateOrderAsync(Order order);
        Task<Order?> GetOrderByIdAsync(string id);
        Task<IEnumerable<Order>> GetOrdersByCustomerIdAsync(string customerId);
        Task<IEnumerable<Order>> GetOrdersByFlightIdAsync(string flightId);
        Task<Order> UpdateOrderAsync(Order order);
        Task DeleteOrderAsync(string id);
        Task<bool> OrderExistsAsync(string id);
    }
}