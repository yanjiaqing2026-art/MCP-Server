@echo off
setlocal enabledelayedexpansion

echo =========================================
echo   E3D MCP Server - Build Script
echo =========================================
echo.

set "PROJECT_DIR=%~dp0"
set "PROJECT_DIR=!PROJECT_DIR:~0,-1!"
cd /d "!PROJECT_DIR!"

rem Check E3D DLLs
set "E3D_DIR=D:\AVEVA\Everything3D3.1"
if not exist "!E3D_DIR!\Aveva.ApplicationFramework.dll" (
    echo [ERROR] E3D not found at !E3D_DIR!
    echo Update E3D_DIR in build.bat if needed.
    pause
    exit /b 1
)
echo [OK] E3D found at !E3D_DIR!

rem .NET Framework targeting pack
set "TP=%ProgramFiles(x86)%\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2"
set "FWARG="
if exist "!TP!\mscorlib.dll" set "FWARG=-p:FrameworkPathOverride=!TP!"

echo.
echo Building MCPServer.dll...
dotnet build MCPServer.csproj -c Release !FWARG!

IF !ERRORLEVEL! NEQ 0 (
    echo [ERROR] Build failed.
    pause
    exit /b 1
)

set "DLL=!PROJECT_DIR!\bin\Release\net472\MCPServer.dll"

echo.
echo =========================================
echo   BUILD SUCCESSFUL
echo =========================================
echo.
echo DLL: !DLL!
echo.
echo ---- Deploy Steps ----
echo 1. Copy MCPServer.dll to %%E3D_INSTALL_DIR%%\E3DAddins\MCPServer\
echo 2. Add to %%E3D_INSTALL_DIR%%\DesignAddins.xml:
echo    ^<string^>E3DAddins\MCPServer^</string^>
echo 3. Restart E3D
echo.
echo MCP Endpoint: http://localhost:8286/sse
echo Health Check: http://localhost:8286/health
echo =========================================
pause
