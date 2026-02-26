FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Homassy.Email/Homassy.Email.csproj", "Homassy.Email/"]
RUN dotnet restore "./Homassy.Email/Homassy.Email.csproj"
COPY . .
WORKDIR "/src/Homassy.Email"
RUN dotnet build "./Homassy.Email.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Homassy.Email.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Homassy.Email.dll"]
