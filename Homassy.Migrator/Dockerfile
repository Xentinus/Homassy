# Build the migrator
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files and restore
COPY ["Homassy.Migrator/Homassy.Migrator.csproj", "Homassy.Migrator/"]
COPY ["Homassy.API/Homassy.API.csproj", "Homassy.API/"]
RUN dotnet restore "Homassy.Migrator/Homassy.Migrator.csproj"

# Copy source and build
COPY . .
WORKDIR "/src/Homassy.Migrator"
RUN dotnet build "Homassy.Migrator.csproj" -c Release -o /app/build

# Publish
FROM build AS publish
RUN dotnet publish "Homassy.Migrator.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime image (lightweight aspnet, NOT sdk)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Homassy.Migrator.dll"]
