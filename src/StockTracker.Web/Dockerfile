FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/StockTracker.Shared/StockTracker.Shared.csproj src/StockTracker.Shared/
COPY src/StockTracker.Web/StockTracker.Web.csproj src/StockTracker.Web/

# Create a simple static file host project
RUN mkdir -p src/StockTracker.WebHost && \
    printf '<Project Sdk="Microsoft.NET.Sdk.Web">\n  <PropertyGroup>\n    <TargetFramework>net9.0</TargetFramework>\n    <ImplicitUsings>enable</ImplicitUsings>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="9.0.0" />\n  </ItemGroup>\n</Project>\n' > src/StockTracker.WebHost/StockTracker.WebHost.csproj && \
    printf 'var builder = WebApplication.CreateBuilder(args);\nvar app = builder.Build();\napp.UseBlazorFrameworkFiles();\napp.UseStaticFiles();\napp.UseRouting();\napp.MapFallbackToFile("index.html");\napp.Run();\n' > src/StockTracker.WebHost/Program.cs

RUN dotnet restore src/StockTracker.Web/StockTracker.Web.csproj
RUN dotnet restore src/StockTracker.WebHost/StockTracker.WebHost.csproj
COPY src/ src/
RUN dotnet publish src/StockTracker.Web/StockTracker.Web.csproj -c Release -o /app/blazor
RUN dotnet publish src/StockTracker.WebHost/StockTracker.WebHost.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
COPY --from=build /app/blazor/wwwroot ./wwwroot
ENV ASPNETCORE_URLS=http://0.0.0.0:80
EXPOSE 80
ENTRYPOINT ["dotnet", "StockTracker.WebHost.dll"]
