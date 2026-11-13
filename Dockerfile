FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /source

COPY ["src/HookHarbor.Api/HookHarbor.Api.csproj", "src/HookHarbor.Api/"]

RUN dotnet restore "src/HookHarbor.Api/HookHarbor.Api.csproj"

COPY . .

RUN dotnet publish "src/HookHarbor.Api/HookHarbor.Api.csproj" --configuration Release --output /app/publish --no-restore /p:UseAppHost=false


FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

WORKDIR /app

USER root

RUN apt-get update && apt-get install --yes --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "HookHarbor.Api.dll"]