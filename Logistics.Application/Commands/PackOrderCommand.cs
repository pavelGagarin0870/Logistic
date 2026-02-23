using Logistics.Application.DispatchR;

namespace Logistics.Application.Commands;

public sealed record PackOrderCommand(Guid OrderId, string WarehouseId, double Weight) : IRequest<Unit>;
