using Logistics.Application.DispatchR;

namespace Logistics.Application.Commands;

public sealed record MarkOrderDeliveredCommand(Guid OrderId, DateTime DeliveredAt) : IRequest<Unit>;
