FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Assistant_BACK.sln ./
COPY Assistant_Api/Assistant_Api.csproj Assistant_Api/
RUN dotnet restore Assistant_Api/Assistant_Api.csproj

COPY . .
RUN dotnet publish Assistant_Api/Assistant_Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Assistant_Api.dll"]
