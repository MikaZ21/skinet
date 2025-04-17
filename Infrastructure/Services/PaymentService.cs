using Core.Entities;
using Core.Entities.OrderAggregate;
using Core.Interfaces;
using Core.Specifications;
using Microsoft.Extensions.Configuration;
using Stripe;
using Product = Core.Entities.Product;

namespace Infrastructure.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IBasketRepository _basketRepository;
        private readonly IUnitOfWork _unitOfWork;

        private readonly IConfiguration _config;
        
        public PaymentService(IBasketRepository basketRepository, 
            IUnitOfWork unitOfWork, 
            IConfiguration config)
                {
                _config = config;
                _unitOfWork = unitOfWork;
                _basketRepository = basketRepository;

                }
        public async Task<CustomerBasket> CreateOrUpdatePaymentIntent(string basketId)
        {
            StripeConfiguration.ApiKey = _config["StripeSettings:SecretKey"];

            var basket = await _basketRepository.GetBasketAsync(basketId);

            if (basket == null) return null;

            var shippingPrice = 0m;

            if (basket.DeliveryMethodId.HasValue)
            {
                var deliveryMethod = await _unitOfWork.Repository<DeliveryMethod>()
                    .GetByIdAsync((int)basket.DeliveryMethodId);
                shippingPrice = deliveryMethod.Price;
            }

            foreach (var item in basket.Items)
            {
                var productItem = await _unitOfWork.Repository<Product>().GetByIdAsync(item.Id);
                if (item.Price != productItem.Price)
                {
                    item.Price = productItem.Price;
                }
            }

            var service = new PaymentIntentService();
            var oldIntentId = basket.PaymentIntentId;
            PaymentIntent intent;

            if (string.IsNullOrEmpty(oldIntentId))
            {
                var createOptions = new PaymentIntentCreateOptions
                {
                    Amount = (long) basket.Items.Sum(i => i.Quantity * (i.Price * 100)) + (long)
                    shippingPrice * 100,
                    Currency = "usd",
                    PaymentMethodTypes = new List<string> {"card"}
                };
                intent = await service.CreateAsync(createOptions);
                basket.PaymentIntentId = intent.Id;
                basket.ClientSecret = intent.ClientSecret;
            }
            else
            {
                var updateOptions = new PaymentIntentUpdateOptions
                {
                    Amount = (long) basket.Items.Sum(i => i.Quantity * (i.Price * 100)) + (long)
                    shippingPrice * 100,
                };
                intent = await service.UpdateAsync(oldIntentId, updateOptions);
            }
            
            await _basketRepository.UpdateBasketAsync(basket);

            var spec = new OrderByPaymentIntentIdSpecification(basketId);
            var order = await _unitOfWork.Repository<Core.Entities.OrderAggregate.Order>()
                    .GetEntityWithSpec(spec);
            if (order != null)
            {
                order.PaymentIntentId = basket.PaymentIntentId;
                _unitOfWork.Repository<Core.Entities.OrderAggregate.Order>().Update(order);
                await _unitOfWork.Complete();
            }

            return basket;
        }

        public async Task<Core.Entities.OrderAggregate.Order> UpdateOrderPaymentFailed(string paymentIntentId)
        {
            var spec = new OrderByPaymentIntentIdSpecification(paymentIntentId);
            var order = await _unitOfWork.Repository<Core.Entities.OrderAggregate.Order>().GetEntityWithSpec(spec);

            if (order == null) return null;

            order.Status = OrderStatus.PaymentFailed;

            order.AddOrderEvent(new OrderEvent
            {
                EventType = "PaymentFailed",
                EventData = paymentIntentId
            });

            _unitOfWork.Repository<Core.Entities.OrderAggregate.Order>().Update(order);
            await _unitOfWork.Complete();

            return order;
        }

        public async Task<Core.Entities.OrderAggregate.Order> UpdateOrderPaymentSucceeded(string paymentIntentId)
        {
            var spec = new OrderByPaymentIntentIdSpecification(paymentIntentId);
            var order = await _unitOfWork.Repository<Core.Entities.OrderAggregate.Order>().GetEntityWithSpec(spec);

            if (order == null) return null;

            order.Status = OrderStatus.PaymentReceived;

            order.AddOrderEvent(new OrderEvent
            {
                EventType = "PaymentSucceeded",
                EventData = paymentIntentId
            });

            _unitOfWork.Repository<Core.Entities.OrderAggregate.Order>().Update(order);
            await _unitOfWork.Complete();

            return order;
        }

            private async Task<List<OrderItem>> BuildOrderItemsFromBasket(CustomerBasket basket)
        {
            var items = new List<OrderItem>();

            foreach (var bItem in basket.Items)
            {
                // Productリポジトリから最新情報を取る
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(bItem.Id);
                var itemOrdered = new ProductItemOrdered(product.Id, product.Name, product.PictureUrl);
                var orderItem   = new OrderItem(itemOrdered, product.Price, bItem.Quantity);
                items.Add(orderItem);
            }

            return items;
        }
    }
}