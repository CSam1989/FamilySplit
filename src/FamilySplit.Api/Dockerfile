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
COPY ["Directory.Packages.props", "./"]
COPY ["src/FamilySplit.Domain/FamilySplit.Domain.csproj", "src/FamilySplit.Domain/"]
COPY ["src/FamilySplit.Common/FamilySplit.Common.csproj", "src/FamilySplit.Common/"]
COPY ["src/FamilySplit.Application/FamilySplit.Application.csproj", "src/FamilySplit.Application/"]
COPY ["src/FamilySplit.Infrastructure/FamilySplit.Infrastructure.csproj", "src/FamilySplit.Infrastructure/"]
COPY ["src/Features/FamilySplit.Features.Activities/FamilySplit.Features.Activities.csproj", "src/Features/FamilySplit.Features.Activities/"]
COPY ["src/Features/FamilySplit.Features.Admin/FamilySplit.Features.Admin.csproj", "src/Features/FamilySplit.Features.Admin/"]
COPY ["src/Features/FamilySplit.Features.Dashboard/FamilySplit.Features.Dashboard.csproj", "src/Features/FamilySplit.Features.Dashboard/"]
COPY ["src/Features/FamilySplit.Features.Expenses/FamilySplit.Features.Expenses.csproj", "src/Features/FamilySplit.Features.Expenses/"]
COPY ["src/Features/FamilySplit.Features.Families/FamilySplit.Features.Families.csproj", "src/Features/FamilySplit.Features.Families/"]
COPY ["src/Features/FamilySplit.Features.Groups/FamilySplit.Features.Groups.csproj", "src/Features/FamilySplit.Features.Groups/"]
COPY ["src/Features/FamilySplit.Features.Notifications/FamilySplit.Features.Notifications.csproj", "src/Features/FamilySplit.Features.Notifications/"]
COPY ["src/Features/FamilySplit.Features.Settlements/FamilySplit.Features.Settlements.csproj", "src/Features/FamilySplit.Features.Settlements/"]
COPY ["src/Features/FamilySplit.Features.Users/FamilySplit.Features.Users.csproj", "src/Features/FamilySplit.Features.Users/"]
COPY ["src/FamilySplit.Api/FamilySplit.Api.csproj", "src/FamilySplit.Api/"]
RUN dotnet restore "src/FamilySplit.Api/FamilySplit.Api.csproj"

COPY . .
RUN dotnet publish "src/FamilySplit.Api/FamilySplit.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "FamilySplit.Api.dll"]
