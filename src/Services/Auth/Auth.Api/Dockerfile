FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY ["src/Services/Auth/Auth.Api/Auth.Api.csproj", "src/Services/Auth/Auth.Api/"]
COPY ["src/BuildingBlocks/Shared.Contracts/Shared.Contracts.csproj", "src/BuildingBlocks/Shared.Contracts/"]
RUN dotnet restore "src/Services/Auth/Auth.Api/Auth.Api.csproj"

COPY . .
RUN dotnet publish "src/Services/Auth/Auth.Api/Auth.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Auth.Api.dll"]
