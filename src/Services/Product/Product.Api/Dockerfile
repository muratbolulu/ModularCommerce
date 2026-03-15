FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY ["src/Services/Product/Product.Api/Product.Api.csproj", "src/Services/Product/Product.Api/"]
COPY ["src/Services/Product/Product.Application/Product.Application.csproj", "src/Services/Product/Product.Application/"]
COPY ["src/Services/Product/Product.Domain/Product.Domain.csproj", "src/Services/Product/Product.Domain/"]
COPY ["src/Services/Product/Product.Infrastructure/Product.Infrastructure.csproj", "src/Services/Product/Product.Infrastructure/"]
COPY ["src/BuildingBlocks/Shared.Contracts/Shared.Contracts.csproj", "src/BuildingBlocks/Shared.Contracts/"]
RUN dotnet restore "src/Services/Product/Product.Api/Product.Api.csproj"

COPY . .
RUN dotnet publish "src/Services/Product/Product.Api/Product.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Product.Api.dll"]
