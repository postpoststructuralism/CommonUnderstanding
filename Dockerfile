# Dockerfile for Common Understanding Application
# This allows containerized deployment to Azure Container Apps or other container platforms

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["CommonUnderstanding/CommonUnderstanding.csproj", "CommonUnderstanding/"]
RUN dotnet restore "CommonUnderstanding/CommonUnderstanding.csproj"
COPY . .
WORKDIR "/src/CommonUnderstanding"
RUN dotnet build "CommonUnderstanding.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "CommonUnderstanding.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "CommonUnderstanding.dll"]
