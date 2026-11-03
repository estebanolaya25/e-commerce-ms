FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Gateway_BACK.sln ./
COPY Gateway_Api/Gateway_Api.csproj Gateway_Api/
RUN dotnet restore Gateway_Api/Gateway_Api.csproj

COPY . .
RUN dotnet publish Gateway_Api/Gateway_Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Gateway_Api.dll"]
