# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT_PATH
WORKDIR /src
COPY . .
RUN dotnet restore "$PROJECT_PATH"
RUN dotnet publish "$PROJECT_PATH" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
ARG ENTRY_ASSEMBLY
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ENTRY_ASSEMBLY=${ENTRY_ASSEMBLY}
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=12 \
    CMD curl -fsS "http://localhost:8080/health" || exit 1
ENTRYPOINT ["sh", "-c", "exec dotnet \"$ENTRY_ASSEMBLY\""]
