# AutoRes - BC Parks Reservation Automation

AutoRes is a command-line tool that automates the reservation process for popular BC Provincial Parks day-use passes. Built with C# and featuring a modern console interface powered by Spectre.Console.

## Features

- 🏔️ Automated reservation for Joffre Lakes and Garibaldi Provincial Parks
- 🎨 Beautiful console UI with Spectre.Console
- 🔍 Availability checking
- 📸 Screenshot capture of successful reservations
- 💾 Saves credentials and preferences for faster booking
- 📅 Remembers last reservation details (date, email, party size)
- 🤖 Self-healing automation that learns from each attempt
- 🔧 Smart selector system that adapts to website changes
- 📊 Success rate tracking and learning statistics
- 🎥 Video recording of reservation attempts
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

## Settings Storage

Your preferences are automatically saved after successful reservations:
- Email address
- Last selected park
- Last reservation date
- Party size

Settings are stored in: `%APPDATA%\AutoRes\settings.json` (Windows) or `~/.config/AutoRes/settings.json` (Linux/Mac)

## Self-Healing Automation

The application learns from each reservation attempt:
- **Smart Selectors**: Automatically tries multiple strategies to find page elements
- **Learning System**: Records which selectors work and prioritizes them in future attempts
- **Debug Screenshots**: Captures screenshots at each step for troubleshooting
- **Video Recording**: Records the entire session for analysis
- **Success Tracking**: Monitors success rates and adapts strategies

Learning data is stored in: `%APPDATA%\AutoRes\Learning\` with separate files for each park.

## Supported Parks

### Joffre Lakes Provincial Park
- Day-use passes required 2 days in advance (starting 7 AM Pacific)
- Popular for turquoise lakes and mountain views
- Maximum 4 people per pass

### Garibaldi Provincial Park
- Vehicle passes required for specific trailheads on certain days
- Three trailheads: Diamond Head, Rubble Creek, Cheakamus Lake
- Different schedules for each trailhead
- Free vehicle passes (no cost)

## Coming Soon

- Support for more BC Parks (Golden Ears, Mount Seymour, etc.)
- Automatic retry mechanisms
- Email notifications
- Scheduled booking attempts

## Disclaimer

This tool is for educational purposes. Please use responsibly and in accordance with BC Parks terms of service.