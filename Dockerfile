FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["Finanzas.Api/Finanzas.Api.csproj", "Finanzas.Api/"]
COPY ["Finanzas.Application/Finanzas.Application.csproj", "Finanzas.Application/"]
COPY ["Finanzas.Domain/Finanzas.Domain.csproj", "Finanzas.Domain/"]
COPY ["Finanzas.Infrastructure/Finanzas.Infrastructure.csproj", "Finanzas.Infrastructure/"]
RUN dotnet restore "Finanzas.Api/Finanzas.Api.csproj"

COPY . .
RUN dotnet publish "Finanzas.Api/Finanzas.Api.csproj" -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Finanzas.Api.dll"]
