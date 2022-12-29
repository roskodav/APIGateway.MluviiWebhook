#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging. 
 
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base 
WORKDIR /app 
RUN apt-get update && apt-get --yes install curl
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build 
WORKDIR /src 
COPY . . 
RUN dotnet restore "APIGateway.MluviiWebhook/APIGateway.MluviiWebhook.csproj" 

WORKDIR "/src/" 
RUN dotnet build "APIGateway.MluviiWebhook/APIGateway.MluviiWebhook.csproj" -c Release -o /app/build 
 
FROM build AS publish 
RUN dotnet publish "APIGateway.MluviiWebhook/APIGateway.MluviiWebhook.csproj" -c Release -o /app/publish -f net6.0
 

RUN dotnet restore "APIGateway.MluviiWebhook.Tests/APIGateway.MluviiWebhook.Tests.csproj"
RUN dotnet build "APIGateway.MluviiWebhook.Tests/APIGateway.MluviiWebhook.Tests.csproj"
RUN dotnet test "APIGateway.MluviiWebhook.Tests/APIGateway.MluviiWebhook.Tests.csproj"

FROM base AS final 
WORKDIR /app 
COPY --from=publish /app/publish . 
ENV ASPNETCORE_ENVIRONMENT="Production" 
ENV ASPNETCORE_URLS="http://0.0.0.0:5025" 
EXPOSE 5025 

HEALTHCHECK --interval=30s --timeout=6s --retries=3 CMD curl --fail http://localhost:5025/health || exit 1

ENTRYPOINT ["dotnet", "APIGateway.MluviiWebhook.dll"] 

