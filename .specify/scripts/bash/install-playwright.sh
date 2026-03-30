#!/bin/bash
# Install Playwright browsers for ClawSharp

# Ensure dotnet is installed
if ! command -v dotnet &> /dev/null
then
    echo "dotnet could not be found. Please install .NET SDK."
    exit 1
fi

echo "Building ClawSharp.Lib to restore dependencies..."
dotnet build ClawSharp.Lib/ClawSharp.Lib.csproj

echo "Installing Playwright browsers..."
# Playwright .NET requires playwright.ps1 or running the tool directly
# Usually, it's installed via: dotnet run --project ClawSharp.Lib -- playwright install
# But for simplicity, we can use the playwright tool if installed or use the direct dotnet command
dotnet run --project ClawSharp.Lib/ClawSharp.Lib.csproj -- playwright install chromium
