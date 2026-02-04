# ===== Base runtime image =====
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Optional: run as non-root user if APP_UID is set
# USER $APP_UID

# ===== Build stage =====
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy only the project file first for restore
COPY ["telegram-killer.API.csproj", "./"]

# Restore NuGet packages
RUN dotnet restore "telegram-killer.API.csproj"

# Copy all source files
COPY . .

# Build the project
RUN dotnet build "telegram-killer.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ===== Publish stage =====
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Publish the project to a folder suitable for runtime
RUN dotnet publish "telegram-killer.API.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    /p:UseAppHost=false

# ===== Final runtime image =====
FROM base AS final
WORKDIR /app

# Copy published output from build/publish stage
COPY --from=publish /app/publish .

# Set entry point
ENTRYPOINT ["dotnet", "telegram-killer.API.dll"]
