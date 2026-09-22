# syntax=docker/dockerfile:1
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY SalemBonus.Domain/SalemBonus.Domain.csproj SalemBonus.Domain/
COPY SalemBonus.Application/SalemBonus.Application.csproj SalemBonus.Application/
COPY SalemBonus.Infrastructure/SalemBonus.Infrastructure.csproj SalemBonus.Infrastructure/
COPY SalemBonus.Api/SalemBonus.Api.csproj SalemBonus.Api/
RUN dotnet restore SalemBonus.Api/SalemBonus.Api.csproj
COPY . .
RUN dotnet publish SalemBonus.Api/SalemBonus.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .
# Render/Koyeb PORT айнымалысын runtime-да береді, әдепкі 8080
EXPOSE 8080
ENTRYPOINT ["sh", "-c", "exec dotnet SalemBonus.Api.dll --urls http://0.0.0.0:${PORT:-8080}"]
