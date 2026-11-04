# Build context: repository root (references Tursib.Contracts)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Tursib.Contracts/Tursib.Contracts.csproj", "Tursib.Contracts/"]
COPY ["TursibIngestion/TursibIngestion.csproj", "TursibIngestion/"]
RUN dotnet restore TursibIngestion/TursibIngestion.csproj
COPY Tursib.Contracts/ Tursib.Contracts/
COPY TursibIngestion/ TursibIngestion/
RUN dotnet publish TursibIngestion/TursibIngestion.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "TursibIngestion.dll"]
