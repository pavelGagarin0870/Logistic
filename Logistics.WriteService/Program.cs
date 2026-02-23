using Logistics.Application;
using Logistics.Infrastructure.Read;
using Logistics.Infrastructure.Write;
using Logistics.WriteService;

var builder = Host.CreateApplicationBuilder(args);

var writeConnection = builder.Configuration.GetConnectionString("WriteDbConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:WriteDbConnection is required.");
var readConnection = builder.Configuration.GetConnectionString("ReadDbConnection")
    ?? throw new InvalidOperationException("ConnectionStrings:ReadDbConnection is required.");

builder.Services.AddWriteInfrastructure(writeConnection);
builder.Services.AddReadInfrastructure(readConnection);
builder.Services.AddApplicationDispatchR();

builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddHostedService<RabbitMqCommandConsumer>();
builder.Services.AddHostedService<EventProjectorWorker>();

var host = builder.Build();
await host.RunAsync();
