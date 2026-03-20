FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8084

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Eventiq.SeatService/Eventiq.SeatService.csproj", "Eventiq.SeatService/"]
COPY ["Eventiq.Contracts/Eventiq.Contracts.csproj", "Eventiq.Contracts/"]
COPY ["Eventiq.Logging/Eventiq.Logging.csproj", "Eventiq.Logging/"]
RUN dotnet restore "Eventiq.SeatService/Eventiq.SeatService.csproj"
COPY . .
WORKDIR "/src/Eventiq.SeatService"
RUN dotnet build "./Eventiq.SeatService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Eventiq.SeatService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Eventiq.SeatService.dll"]
