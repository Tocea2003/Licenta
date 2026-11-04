# Build context: repository root (the backend references Tursib.Contracts)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Tursib.Contracts/Tursib.Contracts.csproj", "Tursib.Contracts/"]
COPY ["TursibBackend/TursibBackend.csproj", "TursibBackend/"]
RUN dotnet restore TursibBackend/TursibBackend.csproj
COPY Tursib.Contracts/ Tursib.Contracts/
COPY TursibBackend/ TursibBackend/
RUN dotnet publish TursibBackend/TursibBackend.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "TursibBackend.dll"]
