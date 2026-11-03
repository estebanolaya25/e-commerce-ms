FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Payments_BACK.sln ./
COPY CapaDTO/CapaDTO.csproj CapaDTO/
COPY CapaDatos/CapaDatos.csproj CapaDatos/
COPY CapaNegocios/CapaNegocios.csproj CapaNegocios/
COPY Payments_Api/Payments_Api.csproj Payments_Api/
RUN dotnet restore Payments_Api/Payments_Api.csproj

COPY . .
RUN dotnet publish Payments_Api/Payments_Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Payments_Api.dll"]
