# syntax=docker/dockerfile:1

# 1) Build do frontend (Vite)
FROM node:24-alpine AS frontend
WORKDIR /app
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# 2) Build da API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src
COPY backend/src/Vtt.Domain/Vtt.Domain.csproj src/Vtt.Domain/
COPY backend/src/Vtt.Application/Vtt.Application.csproj src/Vtt.Application/
COPY backend/src/Vtt.Infrastructure/Vtt.Infrastructure.csproj src/Vtt.Infrastructure/
COPY backend/src/Vtt.Api/Vtt.Api.csproj src/Vtt.Api/
RUN dotnet restore src/Vtt.Api/Vtt.Api.csproj
COPY backend/src/ src/
RUN dotnet publish src/Vtt.Api/Vtt.Api.csproj -c Release -o /app/publish --no-restore

# 3) Imagem final: a API serve o frontend buildado a partir de wwwroot
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=backend /app/publish ./
COPY --from=frontend /app/dist ./wwwroot
# Diretório do volume de uploads, já com o dono certo para o usuário sem privilégios da imagem.
RUN mkdir -p /app/uploads && chown "$APP_UID" /app/uploads
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Vtt.Api.dll"]
