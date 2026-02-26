# Imagen base de ASP.NET 9.0 para tiempo de ejecución
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
# Configurar UTF-8 locale y fuentes
ENV LANG=es_SV.UTF-8
ENV LC_ALL=es_SV.UTF-8
ENV LANGUAGE=es_SV:es
# Instalar fuentes con soporte para caracteres especiales
RUN apt-get update && apt-get install -y \
    fonts-liberation \
    fonts-dejavu \
    fonts-freefont-ttf \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Imagen base de SDK 9.0 para compilación
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
# Configurar UTF-8 locale también en build
ENV LANG=es_SV.UTF-8
ENV LC_ALL=es_SV.UTF-8
ENV LANGUAGE=es_SV:es
# Instalar fuentes en la etapa de build también
RUN apt-get update && apt-get install -y \
    fonts-liberation \
    fonts-dejavu \
    fonts-freefont-ttf \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /src

# Copiar los archivos del proyecto
COPY ["SRAUMOAR.csproj", "./"]
RUN dotnet restore "SRAUMOAR.csproj"
COPY . .
WORKDIR "/src"
RUN dotnet build "SRAUMOAR.csproj" -c Release -o /app/build

# Publicar la aplicación
FROM build AS publish
RUN dotnet publish "SRAUMOAR.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 80
ENV ASPNETCORE_URLS=http://+:80
ENTRYPOINT ["dotnet", "SRAUMOAR.dll"]