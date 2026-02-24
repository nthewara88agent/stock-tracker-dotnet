FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/StockTracker.Shared/*.csproj src/StockTracker.Shared/
COPY src/StockTracker.App/*.csproj src/StockTracker.App/
RUN dotnet restore src/StockTracker.App/StockTracker.App.csproj
COPY src/ src/
RUN dotnet publish src/StockTracker.App/StockTracker.App.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "StockTracker.App.dll"]
