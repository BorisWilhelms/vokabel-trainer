FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy everything and publish
COPY Directory.Build.props .
COPY src/ src/
RUN dotnet publish src/VokabelTrainer.Api/VokabelTrainer.Api.csproj \
    -c Release \
    -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS runtime
WORKDIR /app

# SQLite native deps
RUN apk add --no-cache icu-libs

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app .

VOLUME /app/data

EXPOSE 8080
ENTRYPOINT ["dotnet", "VokabelTrainer.Api.dll"]
