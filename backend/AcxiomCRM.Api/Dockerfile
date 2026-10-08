FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY AcxiomCRM.Api.csproj ./
RUN dotnet restore AcxiomCRM.Api.csproj
COPY . ./
RUN dotnet publish AcxiomCRM.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "AcxiomCRM.Api.dll"]
