FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/EGWNCentral.Api/EGWNCentral.Api.csproj", "src/EGWNCentral.Api/"]
COPY ["src/EGWNCentral.Core/EGWNCentral.Core.csproj", "src/EGWNCentral.Core/"]
COPY ["src/EGWNCentral.Infrastructure/EGWNCentral.Infrastructure.csproj", "src/EGWNCentral.Infrastructure/"]
RUN dotnet restore "src/EGWNCentral.Api/EGWNCentral.Api.csproj"
COPY . .
WORKDIR "/src/src/EGWNCentral.Api"
RUN dotnet build "./EGWNCentral.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./EGWNCentral.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "EGWNCentral.Api.dll"]
