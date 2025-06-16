# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AutoRes is a BC Parks reservation automation tool that helps users book day-use passes for popular provincial parks in British Columbia, Canada. It uses Microsoft Playwright for browser automation and Spectre.Console for an enhanced terminal interface.

## Build and Run Commands

```bash
# Build the project
dotnet build

# Run the application
dotnet run

# First-time setup: Install Playwright browsers
pwsh bin/Debug/net9.0/playwright.ps1 install
```

## Project Structure

- **Framework**: .NET 9.0
- **Main Dependency**: Microsoft.Playwright v1.52.0
- **Entry Point**: Program.cs (uses top-level statements)
- **Output Type**: Console Application (Exe)

## Key Dependencies

- **Microsoft.Playwright**: Browser automation
- **Spectre.Console**: Enhanced console UI

## Architecture

The application follows a service-based architecture:

- `Models/`: Data models for reservations
- `Services/`: Park-specific reservation services implementing `IParkReservationService`
- `Program.cs`: Main entry point with Spectre.Console menu system

Key patterns:
- Async/await for all browser operations
- Service interface for extensibility to other parks
- Progress indicators and rich console output via Spectre.Console

## Development Notes

- Playwright browsers must be installed before first run
- Browser runs in headed mode (visible) for debugging
- Screenshots are saved for successful reservations
- The BC Parks reservation system URL: https://reserve.bcparks.ca/dayuse/
- Day-use passes can be booked 2 days in advance at 7 AM Pacific Time