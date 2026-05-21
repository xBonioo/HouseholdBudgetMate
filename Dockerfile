FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/HouseholdBudgetMate.Abstractions/HouseholdBudgetMate.Abstractions.csproj src/HouseholdBudgetMate.Abstractions/
COPY src/HouseholdBudgetMate.Application/HouseholdBudgetMate.Application.csproj src/HouseholdBudgetMate.Application/
COPY src/HouseholdBudgetMate.Domain/HouseholdBudgetMate.Domain.csproj src/HouseholdBudgetMate.Domain/
COPY src/HouseholdBudgetMate.Migrations/HouseholdBudgetMate.Migrations.csproj src/HouseholdBudgetMate.Migrations/
COPY src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj src/HouseholdBudgetMate.Web/

RUN dotnet restore src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj --runtime linux-x64

COPY . .
RUN dotnet publish src/HouseholdBudgetMate.Web/HouseholdBudgetMate.Web.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained false \
    -p:PublishSingleFile=false \
    -p:UseAppHost=false \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    HOUSEHOLDBUDGETMATE_CONTAINER=true \
    HOUSEHOLDBUDGETMATE_DATA_DIR=/var/lib/householdbudgetmate

RUN mkdir -p /var/lib/householdbudgetmate

EXPOSE 10000

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "HouseholdBudgetMate.Web.dll"]
