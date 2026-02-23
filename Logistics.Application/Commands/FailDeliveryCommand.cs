using Logistics.Application.DispatchR;

namespace Logistics.Application.Commands;

public sealed record FailDeliveryCommand(Guid OrderId, string Reason) : IRequest<Unit>;
