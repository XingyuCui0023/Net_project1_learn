FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY FinanceTracker.sln ./
COPY NuGet.config ./
COPY src/FinanceTracker.Api/FinanceTracker.Api.csproj src/FinanceTracker.Api/
COPY tests/FinanceTracker.Tests/FinanceTracker.Tests.csproj tests/FinanceTracker.Tests/
RUN dotnet restore FinanceTracker.sln --configfile NuGet.config
COPY . .
RUN dotnet publish src/FinanceTracker.Api/FinanceTracker.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "FinanceTracker.Api.dll"]
