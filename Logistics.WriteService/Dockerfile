FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ./../Logistics.slnx ./../Logistics.slnx
COPY ./../Logistics.Domain/ ./../Logistics.Domain/
COPY ./../Logistics.Application/ ./../Logistics.Application/
COPY ./../Logistics.Infrastructure.Write/ ./../Logistics.Infrastructure.Write/
COPY ./../Logistics.Infrastructure.Read/ ./../Logistics.Infrastructure.Read/
COPY ./../Logistics.WriteService/ ./../Logistics.WriteService/

WORKDIR /src/../Logistics.WriteService
RUN dotnet publish Logistics.WriteService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Logistics.WriteService.dll"]

