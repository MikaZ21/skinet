using System.Text.Json;
using Core.Entities;
using Core.Entities.OrderAggregate;
using Core.Interfaces;
using Core.Specifications;

namespace Infrastructure.Services
{
    public class OrderService : IOrderService
    {
        private readonly IBasketRepository _basketRepo;
        private readonly IUnitOfWork _unitOfWork;

        public OrderService(IBasketRepository basketRepo, IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
            _basketRepo = basketRepo;
        }


        public async Task<Order> CreateOrderAsync(string buyerEmail, int deliveryMethodId, string basketId,
        Address shippingAddress)
        {
            // get basket from the repo
            var basket = await _basketRepo.GetBasketAsync(basketId);

            // get items from the product repo
            var items = new List<OrderItem>();
            foreach (var item in basket.Items)
            {
                var productItem = await _unitOfWork.Repository<Product>().GetByIdAsync(item.Id);
                var itemOrdered = new ProductItemOrdered(productItem.Id, productItem.Name, productItem.PictureUrl);
                var orderItem = new OrderItem(itemOrdered, productItem.Price, item.Quantity);
                items.Add(orderItem);
            }

            // get delivery method from repo
            var deliveryMethod = await _unitOfWork.Repository<DeliveryMethod>().GetByIdAsync(deliveryMethodId);

            // calc subtotal
            var subtotal = items.Sum(item => item.Price * item.Quantity);

            // check to see if order exists
            var spec = new OrderByBasketIdSpecification(basketId);
            var order = await _unitOfWork.Repository<Order>().GetEntityWithSpec(spec);

            if (order != null)
            {
                order.ShipToAddress = shippingAddress;
                order.DeliveryMethod = deliveryMethod;
                order.Subtotal = subtotal;

                foreach (var item in items)
                {
                    item.Order = order;
                }

                var updatedData = JsonSerializer.Serialize(
                    items.Select(i => new {
                        ProductId = i.ItemOrdered.ProductItemId,
                        i.Quantity
                    })
                );

                order.AddOrderEvent(new OrderEvent {
                    EventType = "CartUpdated",
                    EventData = updatedData
                });

                foreach (var oldItem in order.OrderItems.ToList())
                {
                    _unitOfWork.Repository<OrderItem>().Delete(oldItem);
                }

                order.UpdateOrderItems(items);
                _unitOfWork.Repository<Order>().Update(order);

            }
            else 
            {
                // create order
                order = new Order(items, buyerEmail, shippingAddress, deliveryMethod, 
                    subtotal, basket.PaymentIntentId);

                order.BasketId = basketId;

                var createdData = JsonSerializer.Serialize(items.Select(i => new {
                    ProductId = i.ItemOrdered.ProductItemId,
                    i.Quantity
                    })
                );
                order.AddOrderEvent(new OrderEvent {
                    EventType = "OrderCreated",
                    EventData = createdData
                });

                _unitOfWork.Repository<Order>().Add(order);
            }

            // TOD O: save to db
            var result = await _unitOfWork.Complete();

            // return order
            return result > 0 ? order : null;
        }

        public async Task<IReadOnlyList<DeliveryMethod>> GetDeliveryMethodsAsync()
        {
            return await _unitOfWork.Repository<DeliveryMethod>().ListAllAsync();
        }

        public async Task<Order> GetOrderByIdAsync(int id, string buyerEmail)
        {
            var spec = new OrderWithItemsAndOrderingSpecification(id, buyerEmail);
            var order = await _unitOfWork.Repository<Order>().GetEntityWithSpec(spec);
            return order;
        }

        public async Task<IReadOnlyList<Order>> GetOrderForUserAsync(string buyerEmail)
        {
            var spec = new OrderWithItemsAndOrderingSpecification(buyerEmail);
            var orders = await _unitOfWork.Repository<Order>().ListAsync(spec);
            return orders;
        }

        private async Task<List<OrderItem>> RebuildOrderItemsFromEventsAsync(Order order)
        {
            // 「OrderCreated」か「CartUpdated」のイベントを最新順に取得
            var latestEvent = order.OrderEvents
                .Where(e => e.EventType == "OrderCreated" || e.EventType == "CartUpdated")
                .OrderBy(e => e.TimeStamp)
                .LastOrDefault();

            if (latestEvent == null) 
                return new List<OrderItem>();

            // JSONデータをパースして商品IDと数量を取得
            var tempItems = JsonSerializer.Deserialize<List<TempOrderEventItem>>(latestEvent.EventData);

            // リポジトリから商品情報を取得し、OrderItemを作成
            var rebuiltItems = new List<OrderItem>();
            foreach (var ti in tempItems)
            {
                var product = await _unitOfWork.Repository<Product>().GetByIdAsync(ti.ProductId);
                var itemOrdered = new ProductItemOrdered(product.Id, product.Name, product.PictureUrl);
                var orderItem = new OrderItem(itemOrdered, product.Price, ti.Quantity);
                rebuiltItems.Add(orderItem);
            }

            return rebuiltItems;
        }
    }

    
}