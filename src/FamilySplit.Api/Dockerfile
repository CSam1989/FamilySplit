# syntax=docker/dockerfile:1.7
ARG DOTNET_VERSION=10.0

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080 \
    DOTNET_RUNNING_IN_CONTAINER=true

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Copy csproj files first for layer caching.
COPY ["Directory.Build.props", "./"]
COPY ["src/FamilySplit.Domain/FamilySplit.Domain.csproj", "src/FamilySplit.Domain/"]
COPY ["src/FamilySplit.Application/FamilySplit.Application.csproj", "src/FamilySplit.Application/"]
COPY ["src/FamilySplit.Infrastructure/FamilySplit.Infrastructure.csproj", "src/FamilySplit.Infrastructure/"]
COPY ["src/FamilySplit.Api/FamilySplit.Api.csproj", "src/FamilySplit.Api/"]
RUN dotnet restore "src/FamilySplit.Api/FamilySplit.Api.csproj"

COPY . .
WORKDIR /src/src/FamilySplit.Api
RUN dotnet publish "FamilySplit.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FamilySplit.Api.dll"]
