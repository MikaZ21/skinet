using System.Text.Json.Serialization;

namespace Core.Entities.OrderAggregate
{
    public class OrderEvent : BaseEntity
    {
        public int OrderId { get; set; }
        [JsonIgnore]
        public Order Order { get; set; }
        public string EventType { get; set; }
        public DateTime TimeStamp { get; set; } = DateTime.UtcNow;
        public string EventData { get; set; }
    }
}