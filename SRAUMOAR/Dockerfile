# Imagen base de ASP.NET 9.0 para tiempo de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Imagen base de SDK 9.0 para compilación
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["SRAUMOAR.csproj", "./"]
RUN dotnet restore "SRAUMOAR.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "SRAUMOAR.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SRAUMOAR.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SRAUMOAR.dll"]
