#!/bin/bash
set -e

echo "Building and running Pension Calculation Engine..."
echo ""

echo "Step 1: Restoring dependencies..."
dotnet restore

echo ""
echo "Step 2: Building solution..."
dotnet build -c Release

echo ""
echo "Step 3: Running unit tests..."
dotnet test --no-build -c Release

echo ""
echo "Step 4: Starting API (Press Ctrl+C to stop)..."
dotnet run --project src/PensionCalculationEngine.Api -c Release --no-build
