using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace AirlineAncillary.Domain.Models
{
    /// <summary>
    /// Represents an airline ancillary offer with 40-minute TTL
    /// </summary>
    public class Offer
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("flightId")]
        [Required]
        public string FlightId { get; set; } = string.Empty;

        [JsonPropertyName("offerType")]
        [Required]
        public string OfferType { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        [Required]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("price")]
        [Required]
        public decimal Price { get; set; }

        [JsonPropertyName("currency")]
        [Required]
        public string Currency { get; set; } = "USD";

        [JsonPropertyName("validUntil")]
        public DateTime ValidUntil { get; set; }

        [JsonPropertyName("status")]
        public OfferStatus Status { get; set; } = OfferStatus.Active;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("ttl")]
        public int TTL { get; set; } = 2400; // 40 minutes in seconds

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Sets the TTL and ValidUntil based on current time + 40 minutes
        /// </summary>
        public void SetTTL()
        {
            var expiryTime = DateTime.UtcNow.AddMinutes(40);
            ValidUntil = expiryTime;
            TTL = 2400; // 40 minutes in seconds
        }

        /// <summary>
        /// Checks if the offer has expired
        /// </summary>
        public bool IsExpired => DateTime.UtcNow > ValidUntil;
    }

    /// <summary>
    /// Offer status enumeration
    /// </summary>
    public enum OfferStatus
    {
        Active,
        Expired,
        Used,
        Cancelled
    }

    /// <summary>
    /// Represents a customer order for airline ancillary services
    /// </summary>
    public class Order
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("customerId")]
        [Required]
        public string CustomerId { get; set; } = string.Empty;

        [JsonPropertyName("flightId")]
        [Required]
        public string FlightId { get; set; } = string.Empty;

        [JsonPropertyName("offerId")]
        [Required]
        public string OfferId { get; set; } = string.Empty;

        [JsonPropertyName("orderItems")]
        public List<OrderItem> OrderItems { get; set; } = new();

        [JsonPropertyName("totalAmount")]
        public decimal TotalAmount { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "USD";

        [JsonPropertyName("status")]
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("confirmedAt")]
        public DateTime? ConfirmedAt { get; set; }

        [JsonPropertyName("confirmationNumber")]
        public string ConfirmationNumber { get; set; } = string.Empty;

        [JsonPropertyName("customerInfo")]
        public CustomerInfo CustomerInfo { get; set; } = new();

        [JsonPropertyName("paymentInfo")]
        public PaymentInfo PaymentInfo { get; set; } = new();

        /// <summary>
        /// Confirms the order and generates confirmation number
        /// </summary>
        public void ConfirmOrder()
        {
            Status = OrderStatus.Confirmed;
            ConfirmedAt = DateTime.UtcNow;
            ConfirmationNumber = GenerateConfirmationNumber();
        }

        private static string GenerateConfirmationNumber()
        {
            return $"AA{DateTime.UtcNow:yyyyMMdd}{Random.Shared.Next(1000, 9999)}";
        }
    }

    /// <summary>
    /// Order status enumeration
    /// </summary>
    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Processing,
        Completed,
        Cancelled,
        Failed
    }

    /// <summary>
    /// Represents an item in an order
    /// </summary>
    public class OrderItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("offerId")]
        public string OfferId { get; set; } = string.Empty;

        [JsonPropertyName("offerType")]
        public string OfferType { get; set; } = string.Empty;

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; } = 1;

        [JsonPropertyName("unitPrice")]
        public decimal UnitPrice { get; set; }

        [JsonPropertyName("totalPrice")]
        public decimal TotalPrice => UnitPrice * Quantity;
    }

    /// <summary>
    /// Customer information
    /// </summary>
    public class CustomerInfo
    {
        [JsonPropertyName("firstName")]
        public string FirstName { get; set; } = string.Empty;

        [JsonPropertyName("lastName")]
        public string LastName { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("phone")]
        public string Phone { get; set; } = string.Empty;
    }

    /// <summary>
    /// Payment information
    /// </summary>
    public class PaymentInfo
    {
        [JsonPropertyName("paymentMethod")]
        public string PaymentMethod { get; set; } = string.Empty;

        [JsonPropertyName("transactionId")]
        public string TransactionId { get; set; } = string.Empty;

        [JsonPropertyName("paymentStatus")]
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

        [JsonPropertyName("paidAt")]
        public DateTime? PaidAt { get; set; }
    }

    /// <summary>
    /// Payment status enumeration
    /// </summary>
    public enum PaymentStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Refunded
    }
}