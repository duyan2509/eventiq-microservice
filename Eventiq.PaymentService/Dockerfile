FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8085

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Eventiq.PaymentService/Eventiq.PaymentService.csproj", "Eventiq.PaymentService/"]
RUN dotnet restore "Eventiq.PaymentService/Eventiq.PaymentService.csproj"
COPY . .
WORKDIR "/src/Eventiq.PaymentService"
RUN dotnet build "./Eventiq.PaymentService.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Eventiq.PaymentService.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# RSA public key for JWT validation (Jwt__PublicKeyPath=/app/keys/public.key)
COPY keys/public.key ./keys/public.key
ENTRYPOINT ["dotnet", "Eventiq.PaymentService.dll"]
