FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY backend/AcxiomCRM.Api/AcxiomCRM.Api.csproj backend/AcxiomCRM.Api/
RUN dotnet restore backend/AcxiomCRM.Api/AcxiomCRM.Api.csproj
COPY backend/AcxiomCRM.Api/ backend/AcxiomCRM.Api/
WORKDIR /src/backend/AcxiomCRM.Api
RUN dotnet publish AcxiomCRM.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AcxiomCRM.Api.dll"]
