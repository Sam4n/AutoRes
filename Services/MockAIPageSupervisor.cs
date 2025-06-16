using Microsoft.Playwright;
using Spectre.Console;

namespace AutoRes.Services;

/// <summary>
/// Mock AI implementation for testing the automation flow
/// This provides intelligent responses without requiring complex AI API setup
/// </summary>
public class MockAIPageSupervisor
{
    private IPage? _page;
    private readonly List<string> _context;
    
    public MockAIPageSupervisor()
    {
        _context = new List<string>();
    }
    
    public void SetPage(IPage page)
    {
        _page = page;
    }
    
    public async Task<string> AnalyzePageContent()
    {
        if (_page == null) return "No page set";
        
        try
        {
            var url = _page.Url;
            var title = await _page.TitleAsync();
            
            // Get key page elements
            var buttons = await _page.Locator("button").AllTextContentsAsync();
            var links = await _page.Locator("a").AllTextContentsAsync();
            var headings = await _page.Locator("h1, h2, h3").AllTextContentsAsync();
            
            return $"URL: {url}\nTitle: {title}\nButtons: {string.Join(", ", buttons.Take(5))}\nLinks: {string.Join(", ", links.Take(5))}\nHeadings: {string.Join(", ", headings.Take(3))}";
        }
        catch (Exception ex)
        {
            return $"Error analyzing page: {ex.Message}";
        }
    }
    
    public async Task<string> AskAI(string question)
    {
        // Mock AI responses based on common scenarios
        if (question.ToLower().Contains("test") && question.ToLower().Contains("successful"))
        {
            return "AI test successful - Mock AI is working properly";
        }
        
        if (question.ToLower().Contains("closed") || question.ToLower().Contains("availability"))
        {
            return "Based on the page analysis, I can see there are still booking options available even though the park may show as 'closed' in general. Look for specific trailhead booking buttons or 'Reserve' links. The system often shows individual trail availability even when the main park appears closed.";
        }
        
        if (question.ToLower().Contains("garibaldi"))
        {
            return "Garibaldi Provincial Park has multiple trailheads available. Look for: 'Diamond Head', 'Rubble Creek', or 'Cheakamus Lake' booking options. Even if the main park shows as closed, individual trailheads may have availability. Click on the specific trailhead you want to book.";
        }
        
        if (question.ToLower().Contains("select") || question.ToLower().Contains("click"))
        {
            return "I can see booking options on the page. Look for buttons or links containing 'Book', 'Reserve', 'Select', or the park name. Click on the one that matches your target park or trailhead.";
        }
        
        if (question.ToLower().Contains("reservation") && question.ToLower().Contains("complete"))
        {
            return "To complete the reservation, fill out the required forms with your details (date, number of people, email). Look for 'Continue', 'Next', or 'Book Now' buttons to proceed through the booking flow.";
        }
        
        return $"Mock AI analyzing: {question.Substring(0, Math.Min(question.Length, 100))}... Based on the page content, I recommend proceeding with the booking process. Look for interactive elements like buttons or forms to continue.";
    }
    
    public async Task<bool> CheckAndHandleParkAvailability(string parkName)
    {
        try
        {
            var pageContent = await AnalyzePageContent();
            
            AnsiConsole.MarkupLine($"[dim]🤖 Mock AI analyzing {parkName} availability...[/]");
            
            // Simulate intelligent analysis
            await Task.Delay(1000); // Simulate processing time
            
            var analysis = await AskAI($"Check availability for {parkName}");
            AnsiConsole.MarkupLine($"[dim]🤖 AI Analysis: {analysis}[/]");
            
            // For Garibaldi, we know it often shows as "closed" but has options
            if (parkName.ToLower().Contains("garibaldi"))
            {
                AnsiConsole.MarkupLine("[green]🤖 Mock AI: Detected Garibaldi - proceeding despite 'closed' status as trailheads may be available[/]");
                return true;
            }
            
            return true; // Mock AI is optimistic and tries to proceed
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error checking availability: {ex.Message}[/]");
            return false;
        }
    }
    
    public async Task<bool> HandleParkSelection(string parkName)
    {
        try
        {
            AnsiConsole.MarkupLine($"[dim]🤖 Mock AI attempting to select {parkName}...[/]");
            
            await Task.Delay(500); // Simulate processing time
            
            var guidance = await AskAI($"How to select {parkName}");
            AnsiConsole.MarkupLine($"[dim]🤖 AI Guidance: {guidance}[/]");
            
            // Mock successful selection
            AnsiConsole.MarkupLine($"[green]🤖 Mock AI: Successfully located {parkName} selection option[/]");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error with park selection: {ex.Message}[/]");
            return false;
        }
    }
    
    public async Task<bool> ExecuteReservationFlow(string parkName, DateTime date, string email, int numberOfPeople)
    {
        try
        {
            AnsiConsole.MarkupLine($"[green]🤖 Mock AI taking control of reservation process...[/]");
            AnsiConsole.MarkupLine($"[dim]  Target: {parkName} on {date:yyyy-MM-dd} for {numberOfPeople} people[/]");
            
            // Simulate AI working through the reservation process
            var steps = new[]
            {
                "Analyzing page structure and forms",
                "Identifying date selection controls",
                "Locating party size options", 
                "Finding email input field",
                "Looking for confirmation buttons",
                "Executing reservation submission"
            };
            
            foreach (var step in steps)
            {
                AnsiConsole.MarkupLine($"[dim]🤖 {step}...[/]");
                await Task.Delay(1000); // Simulate work
            }
            
            // Simulate successful completion
            AnsiConsole.MarkupLine("[green]🤖 Mock AI: Reservation flow completed successfully![/]");
            return true;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error executing reservation flow: {ex.Message}[/]");
            return false;
        }
    }
    
    public void AddContext(string context)
    {
        _context.Add(context);
        AnsiConsole.MarkupLine($"[dim]🤖 Context added: {context}[/]");
    }
    
    public void ClearHistory()
    {
        _context.Clear();
        AnsiConsole.MarkupLine("[dim]🤖 Mock AI context cleared[/]");
    }
}