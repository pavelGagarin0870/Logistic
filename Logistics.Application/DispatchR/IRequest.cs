namespace Logistics.Application.DispatchR;

public interface IRequest;

public interface IRequest<out TResponse> : IRequest;
