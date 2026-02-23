using Logistics.Application.DispatchR;

namespace Logistics.Application.Commands;

public sealed record ChangeAddressCommand(Guid OrderId, string NewAddress) : IRequest<Unit>;
