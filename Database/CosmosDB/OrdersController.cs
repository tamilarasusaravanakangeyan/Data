using Microsoft.AspNetCore.Mvc;
using AirlineAncillary.Domain.Models;
using AirlineAncillary.Infrastructure.Repositories;
using System.ComponentModel.DataAnnotations;

namespace AirlineAncillary.Controllers
{
    /// <summary>
    /// Controller for managing airline ancillary orders
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IOfferRepository _offerRepository;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            IOrderRepository orderRepository,
            IOfferRepository offerRepository,
            ILogger<OrdersController> logger)
        {
            _orderRepository = orderRepository;
            _offerRepository = offerRepository;
            _logger = logger;
        }

        /// <summary>
        /// Create a new order from an offer
        /// </summary>
        /// <param name="request">Order creation request</param>
        /// <returns>Created order</returns>
        [HttpPost]
        [ProducesResponseType(typeof(Order), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Order>> CreateOrder([FromBody] CreateOrderRequest request)
        {
            try
            {
                // Validate offer exists and is not expired
                var offer = await _offerRepository.GetOfferByIdAsync(request.OfferId);
                if (offer == null)
                    return NotFound($"Offer with ID {request.OfferId} not found");

                if (offer.IsExpired || offer.Status != OfferStatus.Active)
                    return BadRequest($"Offer with ID {request.OfferId} is no longer available");

                var order = new Order
                {
                    CustomerId = request.CustomerId,
                    FlightId = offer.FlightId,
                    OfferId = request.OfferId,
                    TotalAmount = request.OrderItems.Sum(item => item.UnitPrice * item.Quantity),
                    Currency = offer.Currency,
                    OrderItems = request.OrderItems.Select(item => new OrderItem
                    {
                        OfferId = request.OfferId,
                        OfferType = offer.OfferType,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice
                    }).ToList(),
                    CustomerInfo = request.CustomerInfo,
                    PaymentInfo = request.PaymentInfo
                };

                var createdOrder = await _orderRepository.CreateOrderAsync(order);

                _logger.LogInformation("Created order {OrderId} for customer {CustomerId}",
                    createdOrder.Id, createdOrder.CustomerId);

                return CreatedAtAction(nameof(GetOrder), new { id = createdOrder.Id }, createdOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order");
                return BadRequest("Failed to create order");
            }
        }

        /// <summary>
        /// Get order by ID
        /// </summary>
        /// <param name="id">Order ID</param>
        /// <returns>Order details</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Order>> GetOrder(string id)
        {
            try
            {
                var order = await _orderRepository.GetOrderByIdAsync(id);

                if (order == null)
                    return NotFound($"Order with ID {id} not found");

                return Ok(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving order {OrderId}", id);
                return NotFound($"Order with ID {id} not found");
            }
        }

        /// <summary>
        /// Get orders for a specific customer
        /// </summary>
        /// <param name="customerId">Customer ID</param>
        /// <returns>List of customer orders</returns>
        [HttpGet("customer/{customerId}")]
        [ProducesResponseType(typeof(IEnumerable<Order>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByCustomer(string customerId)
        {
            try
            {
                var orders = await _orderRepository.GetOrdersByCustomerIdAsync(customerId);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for customer {CustomerId}", customerId);
                return Ok(new List<Order>());
            }
        }

        /// <summary>
        /// Get orders for a specific flight
        /// </summary>
        /// <param name="flightId">Flight ID</param>
        /// <returns>List of flight orders</returns>
        [HttpGet("flight/{flightId}")]
        [ProducesResponseType(typeof(IEnumerable<Order>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByFlight(string flightId)
        {
            try
            {
                var orders = await _orderRepository.GetOrdersByFlightIdAsync(flightId);
                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving orders for flight {FlightId}", flightId);
                return Ok(new List<Order>());
            }
        }

        /// <summary>
        /// Confirm an order and mark offer as used
        /// </summary>
        /// <param name="id">Order ID</param>
        /// <returns>Confirmed order</returns>
        [HttpPost("{id}/confirm")]
        [ProducesResponseType(typeof(Order), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Order>> ConfirmOrder(string id)
        {
            try
            {
                var order = await _orderRepository.GetOrderByIdAsync(id);
                if (order == null)
                    return NotFound($"Order with ID {id} not found");

                if (order.Status != OrderStatus.Pending)
                    return BadRequest($"Order {id} cannot be confirmed in its current state");

                // Confirm the order
                order.ConfirmOrder();
                var updatedOrder = await _orderRepository.UpdateOrderAsync(order);

                // Mark the offer as used
                var offer = await _offerRepository.GetOfferByIdAsync(order.OfferId);
                if (offer != null)
                {
                    offer.Status = OfferStatus.Used;
                    await _offerRepository.UpdateOfferAsync(offer);
                }

                _logger.LogInformation("Confirmed order {OrderId} with confirmation number {ConfirmationNumber}",
                    updatedOrder.Id, updatedOrder.ConfirmationNumber);

                return Ok(updatedOrder);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error confirming order {OrderId}", id);
                return BadRequest("Failed to confirm order");
            }
        }

        /// <summary>
        /// Cancel an order
        /// </summary>
        /// <param name="id">Order ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelOrder(string id)
        {
            try
            {
                var order = await _orderRepository.GetOrderByIdAsync(id);
                if (order == null)
                    return NotFound($"Order with ID {id} not found");

                order.Status = OrderStatus.Cancelled;
                await _orderRepository.UpdateOrderAsync(order);

                _logger.LogInformation("Cancelled order {OrderId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling order {OrderId}", id);
                return NotFound($"Order with ID {id} not found");
            }
        }
    }

    /// <summary>
    /// Request model for creating orders
    /// </summary>
    public class CreateOrderRequest
    {
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [Required]
        public string OfferId { get; set; } = string.Empty;

        [Required]
        public List<OrderItemRequest> OrderItems { get; set; } = new();

        [Required]
        public CustomerInfo CustomerInfo { get; set; } = new();

        [Required]
        public PaymentInfo PaymentInfo { get; set; } = new();
    }

    /// <summary>
    /// Request model for order items
    /// </summary>
    public class OrderItemRequest
    {
        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be at least 1")]
        public int Quantity { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Unit price must be greater than 0")]
        public decimal UnitPrice { get; set; }
    }
}