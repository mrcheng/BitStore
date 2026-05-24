FROM node:22-bookworm-slim AS frontend
WORKDIR /src
COPY package*.json ./
RUN npm ci
COPY frontend ./frontend
COPY vite.config.js ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BitStoreWeb.Net9.csproj ./
RUN dotnet restore BitStoreWeb.Net9.csproj
COPY . ./
COPY --from=frontend /src/wwwroot/dist ./wwwroot/dist
RUN dotnet publish BitStoreWeb.Net9.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
ENTRYPOINT ["dotnet", "BitStoreWeb.Net9.dll"]
