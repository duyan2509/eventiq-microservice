FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Eventiq.EmailService/Eventiq.EmailService.csproj", "Eventiq.EmailService/"]
COPY ["Eventiq.Contracts/Eventiq.Contracts.csproj", "Eventiq.Contracts/"]
RUN dotnet restore "Eventiq.EmailService/Eventiq.EmailService.csproj"
COPY . .
WORKDIR "/src/Eventiq.EmailService"
RUN dotnet build "./Eventiq.EmailService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Eventiq.EmailService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Eventiq.EmailService.dll"]
