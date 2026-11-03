FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Catalog_BACK.sln ./
COPY CapaDTO/CapaDTO.csproj CapaDTO/
COPY CapaDatos/CapaDatos.csproj CapaDatos/
COPY CapaNegocios/CapaNegocios.csproj CapaNegocios/
COPY Catalog_Api/Catalog_Api.csproj Catalog_Api/
RUN dotnet restore Catalog_Api/Catalog_Api.csproj

COPY . .
RUN dotnet publish Catalog_Api/Catalog_Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/imagenes && chown $APP_UID /app/imagenes
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Catalog_Api.dll"]
