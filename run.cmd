@echo off
echo Building and running Pension Calculation Engine...
echo.

echo Step 1: Restoring dependencies...
dotnet restore
if %ERRORLEVEL% NEQ 0 goto error

echo.
echo Step 2: Building solution...
dotnet build -c Release
if %ERRORLEVEL% NEQ 0 goto error

echo.
echo Step 3: Running unit tests...
dotnet test --no-build -c Release
if %ERRORLEVEL% NEQ 0 goto error

echo.
echo Step 4: Starting API (Press Ctrl+C to stop)...
dotnet run --project src\PensionCalculationEngine.Api -c Release --no-build

goto end

:error
echo.
echo ========================================
echo ERROR: Build or test failed!
echo ========================================
exit /b 1

:end
echo.
echo Done!
