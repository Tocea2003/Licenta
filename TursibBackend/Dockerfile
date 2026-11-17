# Contextul de build este RĂDĂCINA repo-ului (backend-ul referă TursibMessaging):
#   docker build -f TursibBackend/Dockerfile .
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
# întâi doar .csproj, ca restore-ul să rămână în cache cât timp nu se schimbă dependențele
COPY TursibMessaging/TursibMessaging.csproj TursibMessaging/
COPY TursibBackend/TursibBackend.csproj TursibBackend/
RUN dotnet restore TursibBackend/TursibBackend.csproj
COPY TursibMessaging/ TursibMessaging/
COPY TursibBackend/ TursibBackend/
RUN dotnet publish TursibBackend/TursibBackend.csproj -c Release --no-restore -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "TursibBackend.dll"]
