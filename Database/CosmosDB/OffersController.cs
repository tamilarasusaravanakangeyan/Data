using Microsoft.AspNetCore.Mvc;
using AirlineAncillary.Domain.Models;
using AirlineAncillary.Infrastructure.Repositories;
using System.ComponentModel.DataAnnotations;

namespace AirlineAncillary.Controllers
{
    /// <summary>
    /// Controller for managing airline ancillary offers
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class OffersController : ControllerBase
    {
        private readonly IOfferRepository _offerRepository;
        private readonly ILogger<OffersController> _logger;

        public OffersController(IOfferRepository offerRepository, ILogger<OffersController> logger)
        {
            _offerRepository = offerRepository;
            _logger = logger;
        }

        /// <summary>
        /// Create a new offer with 40-minute TTL
        /// </summary>
        /// <param name="request">Offer creation request</param>
        /// <returns>Created offer</returns>
        [HttpPost]
        [ProducesResponseType(typeof(Offer), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<Offer>> CreateOffer([FromBody] CreateOfferRequest request)
        {
            try
            {
                var offer = new Offer
                {
                    FlightId = request.FlightId,
                    OfferType = request.OfferType,
                    Title = request.Title,
                    Description = request.Description,
                    Price = request.Price,
                    Currency = request.Currency,
                    Metadata = request.Metadata
                };

                offer.SetTTL(); // Sets 40-minute expiration

                var createdOffer = await _offerRepository.CreateOfferAsync(offer);

                _logger.LogInformation("Created offer {OfferId} for flight {FlightId}",
                    createdOffer.Id, createdOffer.FlightId);

                return CreatedAtAction(nameof(GetOffer), new { id = createdOffer.Id }, createdOffer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating offer");
                return BadRequest("Failed to create offer");
            }
        }

        /// <summary>
        /// Get offer by ID
        /// </summary>
        /// <param name="id">Offer ID</param>
        /// <returns>Offer details</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Offer), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<Offer>> GetOffer(string id)
        {
            try
            {
                var offer = await _offerRepository.GetOfferByIdAsync(id);

                if (offer == null)
                    return NotFound($"Offer with ID {id} not found");

                if (offer.IsExpired)
                {
                    _logger.LogInformation("Offer {OfferId} has expired", id);
                    return NotFound($"Offer with ID {id} has expired");
                }

                return Ok(offer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving offer {OfferId}", id);
                return NotFound($"Offer with ID {id} not found");
            }
        }

        /// <summary>
        /// Get active offers for a specific flight
        /// </summary>
        /// <param name="flightId">Flight ID</param>
        /// <returns>List of active offers</returns>
        [HttpGet("flight/{flightId}")]
        [ProducesResponseType(typeof(IEnumerable<Offer>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Offer>>> GetOffersByFlight(string flightId)
        {
            try
            {
                var offers = await _offerRepository.GetOffersByFlightIdAsync(flightId);
                return Ok(offers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving offers for flight {FlightId}", flightId);
                return Ok(new List<Offer>());
            }
        }

        /// <summary>
        /// Get all active offers
        /// </summary>
        /// <returns>List of all active offers</returns>
        [HttpGet("active")]
        [ProducesResponseType(typeof(IEnumerable<Offer>), StatusCodes.Status200OK)]
        public async Task<ActionResult<IEnumerable<Offer>>> GetActiveOffers()
        {
            try
            {
                var offers = await _offerRepository.GetActiveOffersAsync();
                return Ok(offers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active offers");
                return Ok(new List<Offer>());
            }
        }

        /// <summary>
        /// Cancel an offer
        /// </summary>
        /// <param name="id">Offer ID</param>
        /// <returns>Success status</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CancelOffer(string id)
        {
            try
            {
                var offer = await _offerRepository.GetOfferByIdAsync(id);
                if (offer == null)
                    return NotFound($"Offer with ID {id} not found");

                offer.Status = OfferStatus.Cancelled;
                await _offerRepository.UpdateOfferAsync(offer);

                _logger.LogInformation("Cancelled offer {OfferId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling offer {OfferId}", id);
                return NotFound($"Offer with ID {id} not found");
            }
        }
    }

    /// <summary>
    /// Request model for creating offers
    /// </summary>
    public class CreateOfferRequest
    {
        [Required]
        public string FlightId { get; set; } = string.Empty;

        [Required]
        public string OfferType { get; set; } = string.Empty;

        [Required]
        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }

        public string Currency { get; set; } = "USD";

        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}