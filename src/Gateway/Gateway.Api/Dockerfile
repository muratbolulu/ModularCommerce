FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY ["src/Gateway/Gateway.Api/Gateway.Api.csproj", "src/Gateway/Gateway.Api/"]
RUN dotnet restore "src/Gateway/Gateway.Api/Gateway.Api.csproj"

COPY . .
RUN dotnet publish "src/Gateway/Gateway.Api/Gateway.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Gateway.Api.dll"]
