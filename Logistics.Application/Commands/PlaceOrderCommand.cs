using Logistics.Application.DispatchR;

namespace Logistics.Application.Commands;

public sealed record PlaceOrderCommand(Guid OrderId, string CustomerName, string Address, decimal Total) : IRequest<Unit>;
