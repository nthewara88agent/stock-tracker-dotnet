FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/StockTracker.Shared/StockTracker.Shared.csproj src/StockTracker.Shared/
COPY src/StockTracker.Api/StockTracker.Api.csproj src/StockTracker.Api/
RUN dotnet restore src/StockTracker.Api/StockTracker.Api.csproj
COPY src/ src/
RUN dotnet publish src/StockTracker.Api/StockTracker.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
# Install EF tools for migrations at runtime
ENV ASPNETCORE_URLS=http://0.0.0.0:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "StockTracker.Api.dll"]
