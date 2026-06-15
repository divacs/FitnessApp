FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["FitnessApp.API/FitnessApp.API.csproj", "FitnessApp.API/"]
COPY ["FitnessApp.Application/FitnessApp.Application.csproj", "FitnessApp.Application/"]
COPY ["FitnessApp.Infrastructure/FitnessApp.Infrastructure.csproj", "FitnessApp.Infrastructure/"]
COPY ["FitnessApp.Domain/FitnessApp.Domain.csproj", "FitnessApp.Domain/"]

RUN dotnet restore "FitnessApp.API/FitnessApp.API.csproj"

COPY . .
WORKDIR /src/FitnessApp.API
RUN dotnet publish "FitnessApp.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FitnessApp.API.dll"]
