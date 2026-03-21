FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files and restore
COPY Directory.Build.props .
COPY src/VokabelTrainer.Api/VokabelTrainer.Api.csproj src/VokabelTrainer.Api/
RUN dotnet restore src/VokabelTrainer.Api/VokabelTrainer.Api.csproj

# Copy everything and publish
COPY src/ src/
RUN dotnet publish src/VokabelTrainer.Api/VokabelTrainer.Api.csproj \
    -c Release \
    -o /app \
    --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview-alpine AS runtime
WORKDIR /app

# SQLite native deps
RUN apk add --no-cache icu-libs

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app .

# Data volume for SQLite DB
VOLUME /app/data

EXPOSE 8080
ENTRYPOINT ["dotnet", "VokabelTrainer.Api.dll"]
