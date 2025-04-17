using Core.Specifications;

namespace Core.Entities.OrderAggregate
{
    public class OrderByBasketIdSpecification : BaseSpecification<Order>
    {
        public OrderByBasketIdSpecification(string basketId)
            : base(o => o.BasketId == basketId)
        {
            AddInclude(o => o.OrderItems);
            AddInclude(o => o.DeliveryMethod);
            AddInclude(o => o.ShipToAddress);
            AddInclude(o => o.OrderEvents);
        }
    }
}