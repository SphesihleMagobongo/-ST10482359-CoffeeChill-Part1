# ---------------------------------------------------------------
# Stage 1: Build & publish the .NET 8 isolated Functions worker
# ---------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["CoffeeNChill.Functions.csproj", "./"]
RUN dotnet restore "CoffeeNChill.Functions.csproj"

COPY . .
RUN dotnet publish "CoffeeNChill.Functions.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ---------------------------------------------------------------
# Stage 2: Azure Functions isolated runtime (listens on port 80)
# ---------------------------------------------------------------
FROM mcr.microsoft.com/azure-functions/dotnet-isolated:4-dotnet-isolated8.0 AS final
WORKDIR /home/site/wwwroot

ENV AzureWebJobsScriptRoot=/home/site/wwwroot \
    AzureFunctionsJobHost__Logging__Console__IsEnabled=true \
    FUNCTIONS_WORKER_RUNTIME=dotnet-isolated \
    Storage__Mode=Azurite \
    Storage__MenuTableName=MenuItems \
    Storage__StaffDocsShareName=staff-docs \
    Storage__MaxUploadBytes=10485760

COPY --from=build /app/publish .
