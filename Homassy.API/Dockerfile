# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Homassy.API/Homassy.API.csproj", "Homassy.API/"]
RUN dotnet restore "./Homassy.API/Homassy.API.csproj"
COPY . .
WORKDIR "/src/Homassy.API"
RUN dotnet build "./Homassy.API.csproj" -c $BUILD_CONFIGURATION -o /app/build /p:OpenApiGenerateDocuments=false

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Homassy.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false /p:OpenApiGenerateDocuments=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
# Install curl for healthcheck
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Homassy.API.dll"]