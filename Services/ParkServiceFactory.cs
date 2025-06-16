using AutoRes.Models;
using Spectre.Console;

namespace AutoRes.Services;

public static class ParkServiceFactory
{
    public static IParkReservationService CreateService(string parkName, bool useAI = false)
    {
        // Check if AI is requested and available
        if (useAI && IsAIAvailable())
        {
            return parkName switch
            {
                "Joffre Lakes Provincial Park" => new SmartJoffreLakesReservationService(), // TODO: Create AI-enhanced version
                "Garibaldi Provincial Park" => new AIEnhancedGaribaldiService(),
                _ => throw new NotSupportedException($"AI-enhanced service for '{parkName}' is not yet available")
            };
        }
        
        // Fallback to regular services
        return parkName switch
        {
            "Joffre Lakes Provincial Park" => new SmartJoffreLakesReservationService(),
            "Garibaldi Provincial Park" => new SmartGaribaldiReservationService(),
            _ => throw new NotSupportedException($"Park '{parkName}' is not yet supported")
        };
    }
    
    public static List<string> GetSupportedParks()
    {
        return new List<string>
        {
            "Joffre Lakes Provincial Park",
            "Garibaldi Provincial Park"
        };
    }
    
    public static List<string> GetAIEnabledParks()
    {
        return new List<string>
        {
            "Garibaldi Provincial Park"
        };
    }
    
    public static string GetParkDescription(string parkName)
    {
        return parkName switch
        {
            "Joffre Lakes Provincial Park" => "Day-use passes required 2 days in advance (7 AM Pacific). Famous for turquoise lakes.",
            "Garibaldi Provincial Park" => "Vehicle passes required for specific trailheads. Three locations: Diamond Head, Rubble Creek, Cheakamus Lake.",
            _ => "Description not available"
        };
    }
    
    public static bool IsAIAvailable()
    {
        var apiKey = "YOUR_OPENAI_API_KEY";
        return !string.IsNullOrEmpty(apiKey);
    }
    
    public static void ShowAIConfigurationHelp()
    {
        var panel = new Panel(@"
[bold yellow]AI-Enhanced Automation Setup[/]

To enable AI-powered automation, set your OpenAI API key:

[bold green]Windows (Command Prompt):[/]
set OPENAI_API_KEY=your-api-key-here

[bold green]Windows (PowerShell):[/]
$env:OPENAI_API_KEY=""your-api-key-here""

[bold green]Linux/Mac:[/]
export OPENAI_API_KEY=""your-api-key-here""

[bold green]Or create a .env file:[/]
OPENAI_API_KEY=your-api-key-here

[yellow]Benefits of AI automation:[/]
• Adapts to UI changes automatically
• Intelligent decision making
• Better error handling and recovery
• Handles unexpected scenarios

[yellow]Get an API key at:[/] [link]https://platform.openai.com/api-keys[/]
")
        {
            Header = new PanelHeader("[bold blue]🤖 AI Configuration[/]"),
            Border = BoxBorder.Double,
            Padding = new Padding(1, 0)
        };
        
        AnsiConsole.Write(panel);
    }
}