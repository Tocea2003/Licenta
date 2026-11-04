# Build context: repository root (the simulator references Tursib.Contracts)
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 10000

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Tursib.Contracts/Tursib.Contracts.csproj", "Tursib.Contracts/"]
COPY ["BusSimulator/BusSimulator.csproj", "BusSimulator/"]
RUN dotnet restore BusSimulator/BusSimulator.csproj
COPY Tursib.Contracts/ Tursib.Contracts/
COPY BusSimulator/ BusSimulator/
RUN dotnet publish BusSimulator/BusSimulator.csproj -c Release -o /app/publish --no-restore

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "BusSimulator.dll"]
