using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Core.Entities.OrderAggregate
{
    public class TempOrderEventItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }
}