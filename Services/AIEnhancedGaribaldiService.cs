using Microsoft.Playwright;
using AutoRes.Models;
using Spectre.Console;

namespace AutoRes.Services;

public class AIEnhancedGaribaldiService : IParkReservationService
{
    public string ParkName => "Garibaldi Provincial Park";
    
    private const string ReservationUrl = "https://reserve.bcparks.ca/dayuse/";
    private readonly AutomationLearningService _learningService;
    private readonly MockAIPageSupervisor _aiSupervisor;
    
    public enum Trailhead
    {
        DiamondHead,
        RubbleCreek,
        CheakamusLake
    }
    
    public AIEnhancedGaribaldiService()
    {
        _learningService = new AutomationLearningService(ParkName);
        _aiSupervisor = new MockAIPageSupervisor();
    }
    
    public async Task<ReservationResult> MakeReservationAsync(ParkReservation reservation)
    {
        var result = new ReservationResult();
        _learningService.StartSession(ParkName);
        
        // Ask for trailhead preference if not specified
        var trailhead = await SelectTrailhead();
        
        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn(),
            })
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[green]🤖 AI-Enhanced reservation for Garibaldi - {trailhead}[/]");
                
                try
                {
                    using var playwright = await Playwright.CreateAsync();
                    await using var browser = await playwright.Chromium.LaunchAsync(new()
                    {
                        Headless = false,
                        SlowMo = 100,
                        Args = new[] { "--start-maximized" }
                    });
                    
                    var context = await browser.NewContextAsync(new()
                    {
                        ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                        RecordVideoDir = "videos",
                        RecordVideoSize = new() { Width = 1920, Height = 1080 }
                    });
                    
                    var page = await context.NewPageAsync();
                    
                    // Set the page for AI supervisor
                    _aiSupervisor.SetPage(page);
                    
                    task.Description = "[yellow]🤖 AI navigating to BC Parks[/]";
                    task.Increment(10);
                    
                    _learningService.RecordStep("Navigation", "goto", ReservationUrl);
                    await page.GotoAsync(ReservationUrl);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    
                    await TakeDebugScreenshot(page, "01_initial_page");
                    
                    task.Description = "[yellow]🤖 AI analyzing park options[/]";
                    task.Increment(10);
                    
                    // Let AI handle park selection and availability checking
                    _aiSupervisor.AddContext($"Looking for Garibaldi Provincial Park, specifically {trailhead} trailhead");
                    
                    var parkHandled = await _aiSupervisor.CheckAndHandleParkAvailability(ParkName);
                    
                    if (!parkHandled)
                    {
                        // AI determined park is not available
                        var aiExplanation = await _aiSupervisor.AskAI("What is the current status of Garibaldi Provincial Park? Why can't we proceed with booking?");
                        
                        result.Success = false;
                        result.ErrorMessage = $"AI Analysis: {aiExplanation}";
                        _learningService.EndSession(false, result.ErrorMessage);
                        return;
                    }
                    
                    await TakeDebugScreenshot(page, "02_park_analyzed");
                    
                    task.Description = "[yellow]🤖 AI selecting park and trailhead[/]";
                    task.Increment(20);
                    
                    var parkSelected = await _aiSupervisor.HandleParkSelection($"Garibaldi Provincial Park - {trailhead}");
                    
                    if (!parkSelected)
                    {
                        throw new Exception("AI could not select the park/trailhead");
                    }
                    
                    await TakeDebugScreenshot(page, "03_park_selected");
                    
                    task.Description = "[yellow]🤖 AI handling date and details[/]";
                    task.Increment(20);
                    
                    // Let AI handle the rest of the reservation flow
                    var reservationCompleted = await _aiSupervisor.ExecuteReservationFlow(
                        $"Garibaldi - {trailhead}", 
                        reservation.DesiredDate, 
                        reservation.Email, 
                        reservation.NumberOfPeople);
                    
                    if (reservationCompleted)
                    {
                        task.Description = "[green]🤖 AI completed reservation![/]";
                        task.Increment(40);
                        
                        // Try to get confirmation details
                        var confirmationCheck = await _aiSupervisor.AskAI("Was the reservation successful? If so, what is the confirmation number or reference?");
                        
                        result.Success = true;
                        result.ReservationDate = reservation.DesiredDate;
                        
                        // Extract confirmation number from AI response
                        if (confirmationCheck.Contains("confirmation") || confirmationCheck.Contains("reference"))
                        {
                            result.ConfirmationNumber = ExtractConfirmationFromAIResponse(confirmationCheck);
                        }
                        
                        // Take final screenshot
                        await page.ScreenshotAsync(new() { Path = $"ai_garibaldi_success_{DateTime.Now:yyyyMMdd_HHmmss}.png" });
                        
                        _learningService.EndSession(true, $"AI Success: {confirmationCheck}");
                    }
                    else
                    {
                        var failureReason = await _aiSupervisor.AskAI("What went wrong with the reservation? What should the user know?");
                        
                        result.Success = false;
                        result.ErrorMessage = $"AI couldn't complete reservation: {failureReason}";
                        _learningService.EndSession(false, result.ErrorMessage);
                    }
                    
                    task.Value = 100;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Error during AI-enhanced reservation: {ex.Message}";
                    
                    _learningService.RecordError("AI_General", ex.Message);
                    _learningService.EndSession(false, ex.Message);
                    
                    AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                    
                    // Ask AI for error analysis
                    try
                    {
                        var aiErrorAnalysis = await _aiSupervisor.AskAI($"An error occurred: {ex.Message}. What might have caused this and what should we try differently?");
                        AnsiConsole.MarkupLine($"[yellow]🤖 AI Error Analysis: {aiErrorAnalysis}[/]");
                    }
                    catch { }
                    
                    // Take error screenshot
                    try
                    {
                        using var playwright = await Playwright.CreateAsync();
                        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
                        var page = await browser.NewPageAsync();
                        await TakeDebugScreenshot(page, "ai_error_state");
                    }
                    catch { }
                }
            });
            
        // Display learning statistics and AI insights
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]📊 Learning Statistics:[/]");
        _learningService.DisplayLearningStats();
        
        // Get AI insights about the session
        try
        {
            var aiInsights = await _aiSupervisor.AskAI("Based on this reservation attempt, what insights or recommendations do you have for future bookings?");
            if (!string.IsNullOrEmpty(aiInsights))
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[dim]🤖 AI Insights:[/]");
                AnsiConsole.MarkupLine($"[dim]{aiInsights}[/]");
            }
        }
        catch { }
            
        return result;
    }
    
    private async Task<Trailhead> SelectTrailhead()
    {
        var trailhead = AnsiConsole.Prompt(
            new SelectionPrompt<Trailhead>()
                .Title("[green]Select Garibaldi trailhead:[/]")
                .AddChoices(new[] {
                    Trailhead.DiamondHead,
                    Trailhead.RubbleCreek,
                    Trailhead.CheakamusLake
                })
                .UseConverter(t => t switch
                {
                    Trailhead.DiamondHead => "Diamond Head (June-Oct: Fri-Mon + holidays)",
                    Trailhead.RubbleCreek => "Rubble Creek (June-Oct: Fri-Mon + holidays)",
                    Trailhead.CheakamusLake => "Cheakamus Lake (Apr-Sep: Daily, Sep-Oct: Fri-Mon)",
                    _ => t.ToString()
                }));
        
        return await Task.FromResult(trailhead);
    }
    
    private string ExtractConfirmationFromAIResponse(string aiResponse)
    {
        // Simple extraction logic - could be enhanced
        var words = aiResponse.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var word in words)
        {
            // Look for alphanumeric codes that might be confirmation numbers
            if (word.Length >= 6 && word.Any(char.IsDigit) && word.Any(char.IsLetter))
            {
                return word.Trim(':', '.', ',', '!', '?');
            }
        }
        
        return "AI-Generated";
    }
    
    private async Task TakeDebugScreenshot(IPage page, string name)
    {
        try
        {
            var debugPath = Path.Combine(Directory.GetCurrentDirectory(), "debug");
            Directory.CreateDirectory(debugPath);
            
            var screenshotPath = Path.Combine(debugPath, $"{DateTime.Now:yyyyMMdd_HHmmss}_{name}.png");
            await page.ScreenshotAsync(new() { Path = screenshotPath });
            
            AnsiConsole.MarkupLine($"[dim]📸 Screenshot: {name}[/]");
        }
        catch { }
    }
    
    public async Task<bool> CheckAvailabilityAsync(DateTime date)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();
            
            _aiSupervisor.SetPage(page);
            
            await page.GotoAsync(ReservationUrl);
            
            var result = await _aiSupervisor.CheckAndHandleParkAvailability(ParkName);
            
            return result;
        }
        catch
        {
            return false;
        }
    }
}