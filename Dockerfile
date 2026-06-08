# ── Stage 1: build the TypeScript frontend ──────────────────────────────────
FROM node:22-alpine AS client
WORKDIR /client
COPY client/package.json client/package-lock.json ./
RUN npm ci
COPY client/ ./
RUN npm run build

# ── Stage 2: build the .NET API ──────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS server
WORKDIR /src
COPY Weft.slnx ./
COPY src/Weft.Api/Weft.Api.csproj src/Weft.Api/
RUN dotnet restore src/Weft.Api/Weft.Api.csproj
COPY src/ src/
RUN dotnet publish src/Weft.Api -c Release -o /app /p:UseAppHost=false

# ── Stage 3: runtime — Kestrel serves API + static SPA ───────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=server /app ./
COPY --from=client /client/dist wwwroot/
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=30 \
    CMD wget -qO- http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Weft.Api.dll"]
