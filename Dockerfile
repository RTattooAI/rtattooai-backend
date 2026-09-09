# Backend ASP.NET Core (.NET 10). Build (contexto = raíz del repo):
#   docker build -t rtattooai-backend .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY RTattooAI.slnx ./
COPY src/RTattooAI.Api/RTattooAI.Api.csproj src/RTattooAI.Api/
COPY src/RTattooAI.Application/RTattooAI.Application.csproj src/RTattooAI.Application/
COPY src/RTattooAI.Domain/RTattooAI.Domain.csproj src/RTattooAI.Domain/
COPY src/RTattooAI.Infrastructure/RTattooAI.Infrastructure.csproj src/RTattooAI.Infrastructure/
RUN dotnet restore src/RTattooAI.Api/RTattooAI.Api.csproj

COPY src ./src
RUN dotnet publish src/RTattooAI.Api/RTattooAI.Api.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# La imagen de runtime no trae ningun cliente HTTP: sin esto el healthcheck del
# contenedor falla siempre y el servicio figura "unhealthy" aunque este sano.
RUN apt-get update     && apt-get install -y --no-install-recommends curl     && rm -rf /var/lib/apt/lists/*

RUN useradd --uid 1500 --no-create-home --shell /usr/sbin/nologin appuser
COPY --from=build /app .

# El almacenamiento de imagenes se monta como volumen sobre /data/files. Docker copia
# el propietario del directorio de la imagen al crear el volumen: por eso hay que
# crearlo aqui con el usuario correcto, o el contenedor no podria escribir en el.
RUN mkdir -p /data/files && chown -R appuser:appuser /app /data
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "RTattooAI.Api.dll"]
