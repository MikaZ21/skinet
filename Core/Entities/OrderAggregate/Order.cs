namespace Core.Entities.OrderAggregate
{
    public class Order : BaseEntity
    {
        public Order()
        {
        }
        private readonly string _PaymentIntentId;

        public Order(IReadOnlyList<OrderItem> orderItems, string buyerEmail, 
                    Address shipToAddress, DeliveryMethod deliveryMethod, decimal subtotal, string paymentIntentId)
        {
            BuyerEmail = buyerEmail;
            ShipToAddress = shipToAddress;
            DeliveryMethod = deliveryMethod;
            // OrderItems = orderItems;
            _orderItems = new List<OrderItem>(orderItems);
            Subtotal = subtotal;
            PaymentIntentId = paymentIntentId;
        }

        private readonly List<OrderEvent> _orderEvents = new List<OrderEvent>();
        public IReadOnlyList<OrderEvent> OrderEvents => _orderEvents.AsReadOnly();
        public void AddOrderEvent(OrderEvent orderEvent)
        {
            _orderEvents.Add(orderEvent);
        }
        public void UpdateOrderItems(IEnumerable<OrderItem> newItems)
        {
            _orderItems.Clear();
            _orderItems.AddRange(newItems);
        }
        public string BuyerEmail { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public Address ShipToAddress { get; set; }
        public DeliveryMethod DeliveryMethod { get; set; }
        public IReadOnlyList<OrderItem> OrderItems => _orderItems.AsReadOnly();
        private readonly List<OrderItem> _orderItems;
        public decimal Subtotal { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Pending;
        public string PaymentIntentId { get; set; }
        public decimal GetTotal()
        {
            return Subtotal + DeliveryMethod.Price;
        }
        public string BasketId { get; set; }
    }
}