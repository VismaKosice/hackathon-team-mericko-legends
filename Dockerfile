# Use the latest .NET 10 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file
COPY src/PensionCalculationEngine.Api/PensionCalculationEngine.Api.csproj ./src/PensionCalculationEngine.Api/

# Restore dependencies
RUN dotnet restore src/PensionCalculationEngine.Api/PensionCalculationEngine.Api.csproj

# Copy the rest of the source code
COPY src/ ./src/

# Build and publish the application in Release mode with optimizations for fast cold start
RUN dotnet publish src/PensionCalculationEngine.Api/PensionCalculationEngine.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:PublishTrimmed=false \
    /p:PublishSingleFile=false \
    /p:PublishReadyToRun=true \
    /p:TieredCompilation=true \
    /p:TieredCompilationQuickJit=true

# Use the ASP.NET runtime image for the final stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Copy the published application
COPY --from=build /app/publish .

# Set environment variables optimized for cold start performance
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080
ENV DOTNET_gcServer=1
ENV DOTNET_gcConcurrent=1
ENV DOTNET_TieredPGO=1
ENV DOTNET_ReadyToRun=1
ENV DOTNET_TC_QuickJitForLoops=1
ENV DOTNET_TieredCompilation=1
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port 8080
EXPOSE 8080

# Run the application
ENTRYPOINT ["dotnet", "PensionCalculationEngine.Api.dll"]
