# Contextul de build este RĂDĂCINA repo-ului (serviciul referă TursibMessaging):
#   docker build -f TursibIngestion/Dockerfile .
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY TursibMessaging/TursibMessaging.csproj TursibMessaging/
COPY TursibIngestion/TursibIngestion.csproj TursibIngestion/
RUN dotnet restore TursibIngestion/TursibIngestion.csproj
COPY TursibMessaging/ TursibMessaging/
COPY TursibIngestion/ TursibIngestion/
RUN dotnet publish TursibIngestion/TursibIngestion.csproj -c Release --no-restore -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "TursibIngestion.dll"]
