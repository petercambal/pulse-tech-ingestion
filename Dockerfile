FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY src/PulseTech.Ingestion.Worker/PulseTech.Ingestion.Worker.csproj src/PulseTech.Ingestion.Worker/
RUN dotnet restore src/PulseTech.Ingestion.Worker/PulseTech.Ingestion.Worker.csproj

COPY src/PulseTech.Ingestion.Worker/ src/PulseTech.Ingestion.Worker/
RUN dotnet publish src/PulseTech.Ingestion.Worker/PulseTech.Ingestion.Worker.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "PulseTech.Ingestion.Worker.dll"]
