using Microsoft.Playwright;
using AutoRes.Models;
using Spectre.Console;

namespace AutoRes.Services;

public class JoffreLakesReservationService : IParkReservationService
{
    public string ParkName => "Joffre Lakes Provincial Park";
    
    private const string ReservationUrl = "https://reserve.bcparks.ca/dayuse/";
    private const string ParkId = "joffre-lakes"; // This will need to be confirmed from actual site
    
    public async Task<ReservationResult> MakeReservationAsync(ParkReservation reservation)
    {
        var result = new ReservationResult();
        
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
                var task = ctx.AddTask("[green]Making reservation for Joffre Lakes[/]");
                
                try
                {
                    using var playwright = await Playwright.CreateAsync();
                    await using var browser = await playwright.Chromium.LaunchAsync(new()
                    {
                        Headless = false,
                        SlowMo = 100
                    });
                    
                    var context = await browser.NewContextAsync(new()
                    {
                        ViewportSize = new ViewportSize { Width = 1280, Height = 720 }
                    });
                    
                    var page = await context.NewPageAsync();
                    
                    task.Description = "[yellow]Navigating to BC Parks reservation site[/]";
                    task.Increment(10);
                    
                    await page.GotoAsync(ReservationUrl);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    
                    task.Description = "[yellow]Searching for Joffre Lakes[/]";
                    task.Increment(10);
                    
                    // Look for park selection - this will need adjustment based on actual site
                    var searchInput = page.Locator("input[type='search'], input[placeholder*='park'], input[name*='search']").First;
                    if (await searchInput.IsVisibleAsync())
                    {
                        await searchInput.FillAsync("Joffre Lakes");
                        await page.Keyboard.PressAsync("Enter");
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    }
                    
                    // Select the park from results
                    var parkLink = page.Locator($"text=/Joffre Lakes/i").First;
                    if (await parkLink.IsVisibleAsync())
                    {
                        await parkLink.ClickAsync();
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    }
                    
                    task.Description = "[yellow]Selecting date[/]";
                    task.Increment(20);
                    
                    // Date selection - adjust based on actual calendar implementation
                    var dateString = reservation.DesiredDate.ToString("yyyy-MM-dd");
                    var dateInput = page.Locator("input[type='date'], input[name*='date']").First;
                    if (await dateInput.IsVisibleAsync())
                    {
                        await dateInput.FillAsync(dateString);
                    }
                    else
                    {
                        // Calendar picker logic would go here
                        AnsiConsole.MarkupLine("[yellow]Manual date selection may be required[/]");
                    }
                    
                    task.Description = "[yellow]Selecting number of people[/]";
                    task.Increment(10);
                    
                    // People selection
                    var peopleSelect = page.Locator("select[name*='people'], select[name*='visitors']").First;
                    if (await peopleSelect.IsVisibleAsync())
                    {
                        await peopleSelect.SelectOptionAsync(reservation.NumberOfPeople.ToString());
                    }
                    
                    task.Description = "[yellow]Checking availability[/]";
                    task.Increment(20);
                    
                    // Check availability button
                    var checkButton = page.Locator("button:has-text('Check Availability'), button:has-text('Search')").First;
                    if (await checkButton.IsVisibleAsync())
                    {
                        await checkButton.ClickAsync();
                        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    }
                    
                    // Wait for results
                    await page.WaitForSelectorAsync(".availability-results, .time-slots, .booking-times", new() { Timeout = 10000 });
                    
                    task.Description = "[yellow]Selecting time slot[/]";
                    task.Increment(10);
                    
                    // Select available time slot
                    var timeSlots = await page.Locator(".time-slot:not(.disabled), .booking-time:not(.unavailable)").AllAsync();
                    if (timeSlots.Count > 0)
                    {
                        await timeSlots[0].ClickAsync();
                        
                        // Proceed to booking
                        var bookButton = page.Locator("button:has-text('Book'), button:has-text('Continue')").First;
                        if (await bookButton.IsVisibleAsync())
                        {
                            await bookButton.ClickAsync();
                            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        }
                        
                        task.Description = "[yellow]Entering contact information[/]";
                        task.Increment(10);
                        
                        // Fill contact form
                        await page.FillAsync("input[type='email'], input[name*='email']", reservation.Email);
                        
                        // Complete booking
                        var confirmButton = page.Locator("button:has-text('Confirm'), button:has-text('Complete')").First;
                        if (await confirmButton.IsVisibleAsync())
                        {
                            await confirmButton.ClickAsync();
                            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                        }
                        
                        task.Description = "[green]Reservation completed![/]";
                        task.Increment(10);
                        
                        // Get confirmation number
                        var confirmationElement = page.Locator(".confirmation-number, .booking-reference").First;
                        if (await confirmationElement.IsVisibleAsync())
                        {
                            result.ConfirmationNumber = await confirmationElement.TextContentAsync();
                        }
                        
                        result.Success = true;
                        result.ReservationDate = reservation.DesiredDate;
                        
                        // Take screenshot of confirmation
                        await page.ScreenshotAsync(new() { Path = $"confirmation_{DateTime.Now:yyyyMMdd_HHmmss}.png" });
                    }
                    else
                    {
                        result.Success = false;
                        result.ErrorMessage = "No available time slots found for the selected date.";
                    }
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Error during reservation: {ex.Message}";
                    AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                }
                
                task.Value = 100;
            });
            
        return result;
    }
    
    public async Task<bool> CheckAvailabilityAsync(DateTime date)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
            var page = await browser.NewPageAsync();
            
            await page.GotoAsync(ReservationUrl);
            // Implementation would check for available slots
            
            return true; // Placeholder
        }
        catch
        {
            return false;
        }
    }
}