using Microsoft.AspNetCore.Mvc;
using Core.Entities.OrderAggregate;
using API.Dtos;
using Microsoft.AspNetCore.Authorization;
using Core.Interfaces;
using AutoMapper;
using API.Extensions;
using API.Errors;
using Microsoft.EntityFrameworkCore;   // Include を使うため
using Infrastructure.Data;             // StoreContext がある場所

namespace API.Controllers
{
    [Authorize]
    public class OrdersController : BaseApiController
    {
        private readonly IOrderService _orderService;
        private readonly IMapper _mapper;
        private readonly StoreContext  _ctx;


        public OrdersController(IOrderService orderService, IMapper mapper, StoreContext ctx)
        {
            _mapper = mapper;
            _orderService = orderService;
            _ctx          = ctx;
        }

        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder(OrderDto orderDto)
        {
            var email = HttpContext.User.RetrieveEmailFromPrincipal();

            var address = _mapper.Map<AddressDto, Address>(orderDto.ShipToAddress);

            var order = await _orderService.CreateOrderAsync(email, orderDto.DeliveryMethodId, orderDto.BasketId, address);

            if(order == null) return BadRequest(new ApiResponse(400, "Problem creating order"));

            return Ok(order);
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<OrderToReturnDto>>> GetOrderForUser()
        {
            var email = HttpContext.User.RetrieveEmailFromPrincipal();

            var orders = await _orderService.GetOrderForUserAsync(email);

            return Ok(_mapper.Map<IReadOnlyList<OrderToReturnDto>>(orders));
        }

  // 生の OrderItems レコードを返すデバッグ用エンドポイント
        [HttpGet("raw-items/{orderId}")]
        public async Task<ActionResult> GetRawOrderItems(int orderId)
        {
            // 1) DBから直接OrderItemsを取ってくる
            var items = await _ctx.OrderItems
                                  .Where(oi => oi.OrderId == orderId)
                                  .ToListAsync();

            // 2) 件数と中身を返却 (JSON)
            return Ok(new {
                Count = items.Count,
                Items = items.Select(i => new {
                    i.Id,
                    i.OrderId,
                    i.Price,
                    i.Quantity,
                    ProductId = i.ItemOrdered.ProductItemId
                })
            });
        }
        
        [HttpGet("{id}")]
        public async Task<ActionResult<OrderToReturnDto>> GetOrderByIdForUser(int id)
        {
            var email = HttpContext.User.RetrieveEmailFromPrincipal();

            var order = await _orderService.GetOrderByIdAsync(id, email);

            if (order == null) return NotFound(new ApiResponse(404));

            return _mapper.Map<OrderToReturnDto>(order);
        }

        [HttpGet("deliveryMethods")]
        public async Task<ActionResult<IReadOnlyList<DeliveryMethod>>> GetDeliveryMethods()
        {
            return Ok(await _orderService.GetDeliveryMethodsAsync());
        }
    }
}