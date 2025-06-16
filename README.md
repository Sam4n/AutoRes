# AutoRes - BC Parks Reservation Automation

AutoRes is a command-line tool that automates the reservation process for popular BC Provincial Parks day-use passes. Built with C# and featuring a modern console interface powered by Spectre.Console.

## Features

- 🏔️ Automated reservation for Joffre Lakes Provincial Park
- 🎨 Beautiful console UI with Spectre.Console
- 🔍 Availability checking
- 📸 Screenshot capture of successful reservations
- 🚀 Extensible architecture for adding more parks

## Prerequisites

- .NET 8.0 SDK
- Windows, macOS, or Linux
- Internet connection

## Installation

1. Clone the repository
2. Install Playwright browsers:
   ```bash
   dotnet build
   pwsh bin/Debug/net8.0/playwright.ps1 install
   ```

## Usage

Run the application:
```bash
dotnet run
```

Navigate through the menu system to:
- Make a reservation (currently supports Joffre Lakes)
- Check availability for specific dates
- View saved credentials (coming soon)

## Important Notes

- BC Parks day-use passes can be booked 2 days in advance starting at 7 AM Pacific Time
- Maximum 4 passes per transaction
- Passes are non-transferable
- The browser window will open during the reservation process - do not close it

## Architecture

- `Models/`: Data models for park reservations
- `Services/`: Park-specific reservation services
- `Program.cs`: Main entry point with menu system

## Coming Soon

- Support for more BC Parks (Garibaldi, Golden Ears, etc.)
- Credential storage for faster booking
- Automatic retry mechanisms
- Email notifications

## Disclaimer

This tool is for educational purposes. Please use responsibly and in accordance with BC Parks terms of service.