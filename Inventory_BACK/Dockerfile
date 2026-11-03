FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Inventory_BACK.sln ./
COPY CapaDTO/CapaDTO.csproj CapaDTO/
COPY CapaDatos/CapaDatos.csproj CapaDatos/
COPY CapaNegocios/CapaNegocios.csproj CapaNegocios/
COPY Inventory_Api/Inventory_Api.csproj Inventory_Api/
RUN dotnet restore Inventory_Api/Inventory_Api.csproj

COPY . .
RUN dotnet publish Inventory_Api/Inventory_Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Inventory_Api.dll"]
