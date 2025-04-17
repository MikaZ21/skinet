using API.Errors;
using Core.Entities;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Stripe;

namespace API.Controllers
{
    public class PaymentsController : BaseApiController
    {
        private const string WhSecret = "whsec_5702c35b9094b04cb5b42ffbdf7915c53ba555e41ea7f5690bfd82d81a980059";
        private readonly IPaymentService _paymentService;
        private readonly IOrderService   _orderService;
        private readonly ILogger<PaymentsController> _logger;
        public PaymentsController(
            IPaymentService paymentService, 
            IOrderService orderService,
            ILogger<PaymentsController> logger)
        {
            _paymentService = paymentService;
            _orderService   = orderService;
            _logger = logger;
        }

        [Authorize]
        [HttpPost("{basketId}")]
        public async Task<ActionResult<CustomerBasket>> CreateOrUpdatePaymentIntent(string basketId)
        {
            var basket = await _paymentService.CreateOrUpdatePaymentIntent(basketId);

            if (basket == null) 
            return BadRequest(new ApiResponse(400, "Problem with your basket"));
            
            return basket;
        }

        [HttpPost("webhook")]
        public async Task<ActionResult> StripeWebhook()
        {
            var json = await new StreamReader(Request.Body).ReadToEndAsync();

            var stripeEvent = EventUtility.ConstructEvent(json, Request.Headers["Stripe-Signature"], WhSecret, throwOnApiVersionMismatch: false);
            // var stripeEvent = JsonConvert.DeserializeObject<Event>(json);

            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                {
                    var intent = (PaymentIntent)stripeEvent.Data.Object;
                    _logger.LogInformation("Payment succeeded: {IntentId}", intent.Id);

                    var order = await _paymentService.UpdateOrderPaymentSucceeded(intent.Id);

                    if (order == null)
                    {
                         _logger.LogWarning("No order found for PaymentIntentId {IntentId}", order.Id);
                    }
                    await _orderService.CreateOrderAsync(
                        order.BuyerEmail,
                        order.DeliveryMethod.Id,
                        order.BasketId,
                        order.ShipToAddress);

                    _logger.LogInformation("Order {OrderId} updated to PaymentReceived: ", order.Id);
                    break;
                }
               case "payment_intent.payment_failed":
                {
                    var intent = (PaymentIntent)stripeEvent.Data.Object;
                    _logger.LogInformation("Payment failed: {IntentId}", intent.Id);

                    var order = await _paymentService.UpdateOrderPaymentFailed(intent.Id);
                    if (order == null)
                    {
                        _logger.LogWarning("No order found for failed PaymentIntentId {IntentId}", intent.Id);
                        break;
                    }

                    _logger.LogInformation("Order {OrderId} updated to PaymentFailed", order.Id);
                    break;
                }
            }
            return new EmptyResult();
        }
    }
}