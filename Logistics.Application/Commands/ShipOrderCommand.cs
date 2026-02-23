using Logistics.Application.DispatchR;

namespace Logistics.Application.Commands;

public sealed record ShipOrderCommand(Guid OrderId, string CourierName) : IRequest<Unit>;
