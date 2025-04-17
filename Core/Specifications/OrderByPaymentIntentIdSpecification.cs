using System.Linq.Expressions;
using System.Security.Cryptography.X509Certificates;
using Core.Entities.OrderAggregate;

namespace Core.Specifications
{
    public class OrderByPaymentIntentIdSpecification : BaseSpecification<Order>
    {
        public OrderByPaymentIntentIdSpecification(string paymentIntentId)
             : base(o => o.PaymentIntentId == paymentIntentId)
        {
             AddInclude(o => o.OrderItems);
            AddInclude(o => o.DeliveryMethod);
            AddInclude(o => o.ShipToAddress);
            AddInclude(o => o.OrderEvents);
        }
    }
}