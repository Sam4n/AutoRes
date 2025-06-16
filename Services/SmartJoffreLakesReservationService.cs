using Microsoft.Playwright;
using AutoRes.Models;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace AutoRes.Services;

public class SmartJoffreLakesReservationService : IParkReservationService
{
    public string ParkName => "Joffre Lakes Provincial Park";
    
    private const string ReservationUrl = "https://reserve.bcparks.ca/dayuse/";
    private readonly AutomationLearningService _learningService;
    
    public SmartJoffreLakesReservationService()
    {
        _learningService = new AutomationLearningService(ParkName);
    }
    
    public async Task<ReservationResult> MakeReservationAsync(ParkReservation reservation)
    {
        var result = new ReservationResult();
        _learningService.StartSession(ParkName);
        
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
                var task = ctx.AddTask("[green]Making smart reservation for Joffre Lakes[/]");
                
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
                    
                    // Enable console logging
                    page.Console += (_, msg) => AnsiConsole.MarkupLine($"[dim]Browser: {msg.Text}[/]");
                    
                    task.Description = "[yellow]Navigating to BC Parks reservation site[/]";
                    task.Increment(10);
                    
                    _learningService.RecordStep("Navigation", "goto", ReservationUrl);
                    await page.GotoAsync(ReservationUrl);
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    
                    // Take screenshot after navigation
                    await TakeDebugScreenshot(page, "01_initial_page");
                    
                    task.Description = "[yellow]Searching for Joffre Lakes[/]";
                    task.Increment(10);
                    
                    // Try multiple strategies to find the park
                    var parkFound = await TryFindAndSelectPark(page);
                    
                    if (!parkFound)
                    {
                        throw new Exception("Could not find Joffre Lakes in the park list");
                    }
                    
                    await TakeDebugScreenshot(page, "02_park_selected");
                    
                    task.Description = "[yellow]Selecting date[/]";
                    task.Increment(20);
                    
                    var dateSelected = await TrySelectDate(page, reservation.DesiredDate);
                    
                    if (!dateSelected)
                    {
                        throw new Exception("Could not select the desired date");
                    }
                    
                    await TakeDebugScreenshot(page, "03_date_selected");
                    
                    task.Description = "[yellow]Selecting number of people[/]";
                    task.Increment(10);
                    
                    var peopleSelected = await TrySelectPeople(page, reservation.NumberOfPeople);
                    
                    if (!peopleSelected)
                    {
                        throw new Exception("Could not select number of people");
                    }
                    
                    await TakeDebugScreenshot(page, "04_people_selected");
                    
                    task.Description = "[yellow]Checking availability[/]";
                    task.Increment(20);
                    
                    var availabilityChecked = await TryCheckAvailability(page);
                    
                    if (!availabilityChecked)
                    {
                        throw new Exception("Could not check availability");
                    }
                    
                    await TakeDebugScreenshot(page, "05_availability_checked");
                    
                    task.Description = "[yellow]Selecting time slot[/]";
                    task.Increment(10);
                    
                    var timeSlotSelected = await TrySelectTimeSlot(page);
                    
                    if (!timeSlotSelected)
                    {
                        result.ErrorMessage = "No available time slots found for the selected date.";
                        _learningService.EndSession(false, result.ErrorMessage);
                        return;
                    }
                    
                    await TakeDebugScreenshot(page, "06_time_slot_selected");
                    
                    task.Description = "[yellow]Entering contact information[/]";
                    task.Increment(10);
                    
                    var contactEntered = await TryEnterContactInfo(page, reservation.Email);
                    
                    if (!contactEntered)
                    {
                        throw new Exception("Could not enter contact information");
                    }
                    
                    await TakeDebugScreenshot(page, "07_contact_entered");
                    
                    task.Description = "[yellow]Completing reservation[/]";
                    task.Increment(10);
                    
                    var reservationCompleted = await TryCompleteReservation(page);
                    
                    if (reservationCompleted)
                    {
                        task.Description = "[green]Reservation completed![/]";
                        
                        // Get confirmation number
                        var confirmationNumber = await TryGetConfirmationNumber(page);
                        
                        result.Success = true;
                        result.ConfirmationNumber = confirmationNumber;
                        result.ReservationDate = reservation.DesiredDate;
                        
                        // Take final screenshot
                        await page.ScreenshotAsync(new() { Path = $"confirmation_{DateTime.Now:yyyyMMdd_HHmmss}.png" });
                        
                        _learningService.EndSession(true, $"Confirmation: {confirmationNumber}");
                    }
                    else
                    {
                        throw new Exception("Could not complete reservation");
                    }
                    
                    task.Value = 100;
                }
                catch (Exception ex)
                {
                    result.Success = false;
                    result.ErrorMessage = $"Error during reservation: {ex.Message}";
                    
                    _learningService.RecordError("General", ex.Message);
                    _learningService.EndSession(false, ex.Message);
                    
                    AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
                    
                    // Try to take error screenshot
                    try
                    {
                        var page = ctx.GetType().GetProperty("Page")?.GetValue(ctx) as IPage;
                        if (page != null)
                        {
                            await TakeDebugScreenshot(page, "error_state");
                        }
                    }
                    catch { }
                }
            });
            
        // Display learning statistics
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Learning Statistics:[/]");
        _learningService.DisplayLearningStats();
            
        return result;
    }
    
    private async Task<bool> TryFindAndSelectPark(IPage page)
    {
        _learningService.RecordStep("FindPark", "search_and_select");
        
        // Strategy 1: Look for a search input
        var selectors = _learningService.GetBestSelectors("FindPark");
        
        // Add default selectors if none exist
        if (!selectors.Any())
        {
            selectors = new List<ElementSelector>
            {
                new() { Type = "css", Value = "input[type='search']", Priority = 1 },
                new() { Type = "css", Value = "input[placeholder*='park']", Priority = 1 },
                new() { Type = "css", Value = "input[placeholder*='search']", Priority = 1 },
                new() { Type = "css", Value = "#park-search", Priority = 1 },
                new() { Type = "text", Value = "Search", Priority = 1 }
            };
        }
        
        foreach (var selector in selectors)
        {
            try
            {
                ILocator? element = null;
                
                switch (selector.Type)
                {
                    case "css":
                        element = page.Locator(selector.Value);
                        break;
                    case "text":
                        element = page.GetByText(selector.Value);
                        break;
                    case "role":
                        element = page.GetByRole(AriaRole.Searchbox);
                        break;
                }
                
                if (element != null && await element.IsVisibleAsync())
                {
                    await element.FillAsync("Joffre Lakes");
                    await page.Keyboard.PressAsync("Enter");
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    
                    _learningService.AddSelector("FindPark", selector.Type, selector.Value, true);
                    _learningService.RecordSuccess("FindPark");
                    
                    // Now try to click on Joffre Lakes from results
                    return await TryClickParkFromResults(page);
                }
            }
            catch
            {
                _learningService.AddSelector("FindPark", selector.Type, selector.Value, false);
            }
        }
        
        // Strategy 2: Look for dropdown or select
        try
        {
            var parkSelect = page.Locator("select").First;
            if (await parkSelect.IsVisibleAsync())
            {
                var options = await parkSelect.Locator("option").AllTextContentsAsync();
                var joffreOption = options.FirstOrDefault(o => o.Contains("Joffre", StringComparison.OrdinalIgnoreCase));
                
                if (joffreOption != null)
                {
                    await parkSelect.SelectOptionAsync(new[] { joffreOption });
                    _learningService.AddSelector("FindPark", "css", "select", true);
                    _learningService.RecordSuccess("FindPark");
                    return true;
                }
            }
        }
        catch { }
        
        // Strategy 3: Direct link clicking
        return await TryClickParkFromResults(page);
    }
    
    private async Task<bool> TryClickParkFromResults(IPage page)
    {
        var linkSelectors = new[]
        {
            "text=/Joffre Lakes/i",
            "text=/Pipi7íyekw/i", // Indigenous name
            "[href*='joffre']",
            "a:has-text('Joffre')",
            "button:has-text('Joffre')"
        };
        
        foreach (var selector in linkSelectors)
        {
            try
            {
                var element = page.Locator(selector).First;
                if (await element.IsVisibleAsync())
                {
                    await element.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    
                    _learningService.AddSelector("ClickPark", "css", selector, true);
                    return true;
                }
            }
            catch { }
        }
        
        return false;
    }
    
    private async Task<bool> TrySelectDate(IPage page, DateTime date)
    {
        _learningService.RecordStep("SelectDate", "date_selection", date.ToString("yyyy-MM-dd"));
        
        // Strategy 1: Date input field
        var dateInputSelectors = new[]
        {
            "input[type='date']",
            "input[name*='date']",
            "input[placeholder*='date']",
            "#reservation-date",
            "[data-testid*='date']"
        };
        
        foreach (var selector in dateInputSelectors)
        {
            try
            {
                var element = page.Locator(selector).First;
                if (await element.IsVisibleAsync())
                {
                    await element.FillAsync(date.ToString("yyyy-MM-dd"));
                    _learningService.AddSelector("SelectDate", "css", selector, true);
                    _learningService.RecordSuccess("SelectDate");
                    return true;
                }
            }
            catch { }
        }
        
        // Strategy 2: Calendar picker
        try
        {
            // Look for calendar trigger
            var calendarTriggers = new[]
            {
                "[aria-label*='calendar']",
                "[aria-label*='date']",
                "button[class*='calendar']",
                "svg[class*='calendar']",
                ".date-picker-trigger"
            };
            
            foreach (var trigger in calendarTriggers)
            {
                try
                {
                    var element = page.Locator(trigger).First;
                    if (await element.IsVisibleAsync())
                    {
                        await element.ClickAsync();
                        await page.WaitForTimeoutAsync(500);
                        
                        // Try to navigate calendar and select date
                        var dateString = date.Day.ToString();
                        var monthYear = date.ToString("MMMM yyyy");
                        
                        // Check if we need to navigate months
                        var currentMonthElement = page.Locator("[class*='month-year'], [class*='calendar-title']").First;
                        if (await currentMonthElement.IsVisibleAsync())
                        {
                            var currentMonth = await currentMonthElement.TextContentAsync();
                            if (!currentMonth?.Contains(monthYear) ?? true)
                            {
                                // Navigate to correct month
                                // This is simplified - real implementation would be more complex
                            }
                        }
                        
                        // Click the date
                        var dateElement = page.Locator($"[aria-label*='{dateString}'], button:has-text('{dateString}')").First;
                        if (await dateElement.IsVisibleAsync())
                        {
                            await dateElement.ClickAsync();
                            _learningService.AddSelector("SelectDate", "calendar", trigger, true);
                            _learningService.RecordSuccess("SelectDate");
                            return true;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        
        return false;
    }
    
    private async Task<bool> TrySelectPeople(IPage page, int numberOfPeople)
    {
        _learningService.RecordStep("SelectPeople", "select", numberOfPeople.ToString());
        
        var peopleSelectors = new[]
        {
            "select[name*='people']",
            "select[name*='visitors']",
            "select[name*='party']",
            "#num-visitors",
            "[aria-label*='number of people']",
            "select:has-text('How many')"
        };
        
        foreach (var selector in peopleSelectors)
        {
            try
            {
                var element = page.Locator(selector).First;
                if (await element.IsVisibleAsync())
                {
                    await element.SelectOptionAsync(numberOfPeople.ToString());
                    _learningService.AddSelector("SelectPeople", "css", selector, true);
                    _learningService.RecordSuccess("SelectPeople");
                    return true;
                }
            }
            catch { }
        }
        
        // Try input field
        var inputSelectors = new[]
        {
            "input[name*='people']",
            "input[type='number']",
            "input[placeholder*='people']"
        };
        
        foreach (var selector in inputSelectors)
        {
            try
            {
                var element = page.Locator(selector).First;
                if (await element.IsVisibleAsync())
                {
                    await element.FillAsync(numberOfPeople.ToString());
                    _learningService.AddSelector("SelectPeople", "css", selector, true);
                    _learningService.RecordSuccess("SelectPeople");
                    return true;
                }
            }
            catch { }
        }
        
        return false;
    }
    
    private async Task<bool> TryCheckAvailability(IPage page)
    {
        _learningService.RecordStep("CheckAvailability", "click");
        
        var buttonSelectors = new[]
        {
            "button:has-text('Check Availability')",
            "button:has-text('Search')",
            "button:has-text('Find')",
            "[type='submit']",
            "button[class*='search']",
            "button[class*='availability']",
            "#check-availability"
        };
        
        foreach (var selector in buttonSelectors)
        {
            try
            {
                var element = page.Locator(selector).First;
                if (await element.IsVisibleAsync() && await element.IsEnabledAsync())
                {
                    await element.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    
                    // Wait for results
                    await page.WaitForSelectorAsync("[class*='availability'], [class*='time-slot'], [class*='booking']", 
                        new() { Timeout = 10000 });
                    
                    _learningService.AddSelector("CheckAvailability", "css", selector, true);
                    _learningService.RecordSuccess("CheckAvailability");
                    return true;
                }
            }
            catch { }
        }
        
        return false;
    }
    
    private async Task<bool> TrySelectTimeSlot(IPage page)
    {
        _learningService.RecordStep("SelectTimeSlot", "click");
        
        // Look for available time slots
        var slotSelectors = new[]
        {
            ".time-slot:not(.disabled):not(.unavailable)",
            ".booking-time:not(.unavailable)",
            "[class*='slot']:not([class*='disabled'])",
            "button[class*='available']",
            "[data-available='true']",
            ".availability-slot.available"
        };
        
        foreach (var selector in slotSelectors)
        {
            try
            {
                var elements = await page.Locator(selector).AllAsync();
                if (elements.Count > 0)
                {
                    // Prefer morning slots
                    ILocator? selectedSlot = null;
                    foreach (var slot in elements)
                    {
                        var text = await slot.TextContentAsync() ?? "";
                        if (text.Contains("AM") || text.Contains("Morning"))
                        {
                            selectedSlot = slot;
                            break;
                        }
                    }
                    
                    // If no morning slot, take the first available
                    selectedSlot ??= elements[0];
                    
                    await selectedSlot.ClickAsync();
                    await page.WaitForTimeoutAsync(500);
                    
                    _learningService.AddSelector("SelectTimeSlot", "css", selector, true);
                    _learningService.RecordSuccess("SelectTimeSlot");
                    
                    // Click continue/next button
                    await TryClickContinue(page);
                    
                    return true;
                }
            }
            catch { }
        }
        
        return false;
    }
    
    private async Task<bool> TryClickContinue(IPage page)
    {
        var continueSelectors = new[]
        {
            "button:has-text('Continue')",
            "button:has-text('Next')",
            "button:has-text('Book')",
            "button:has-text('Proceed')",
            "button[type='submit']"
        };
        
        foreach (var selector in continueSelectors)
        {
            try
            {
                var element = page.Locator(selector).First;
                if (await element.IsVisibleAsync() && await element.IsEnabledAsync())
                {
                    await element.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    return true;
                }
            }
            catch { }
        }
        
        return false;
    }
    
    private async Task<bool> TryEnterContactInfo(IPage page, string email)
    {
        _learningService.RecordStep("EnterContact", "fill", email);
        
        var emailSelectors = new[]
        {
            "input[type='email']",
            "input[name*='email']",
            "input[placeholder*='email']",
            "#email",
            "[aria-label*='email']"
        };
        
        foreach (var selector in emailSelectors)
        {
            try
            {
                var element = page.Locator(selector).First;
                if (await element.IsVisibleAsync())
                {
                    await element.FillAsync(email);
                    _learningService.AddSelector("EnterContact", "css", selector, true);
                    _learningService.RecordSuccess("EnterContact");
                    return true;
                }
            }
            catch { }
        }
        
        return false;
    }
    
    private async Task<bool> TryCompleteReservation(IPage page)
    {
        _learningService.RecordStep("CompleteReservation", "click");
        
        var completeSelectors = new[]
        {
            "button:has-text('Complete')",
            "button:has-text('Confirm')",
            "button:has-text('Reserve')",
            "button:has-text('Submit')",
            "button[type='submit']:not([disabled])"
        };
        
        foreach (var selector in completeSelectors)
        {
            try
            {
                var element = page.Locator(selector).First;
                if (await element.IsVisibleAsync() && await element.IsEnabledAsync())
                {
                    await element.ClickAsync();
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    
                    _learningService.AddSelector("CompleteReservation", "css", selector, true);
                    _learningService.RecordSuccess("CompleteReservation");
                    return true;
                }
            }
            catch { }
        }
        
        return false;
    }
    
    private async Task<string?> TryGetConfirmationNumber(IPage page)
    {
        var confirmationSelectors = new[]
        {
            ".confirmation-number",
            ".booking-reference",
            "[class*='confirmation']",
            "[class*='reference']",
            "h1:has-text('Confirmation')",
            "p:has-text('confirmation')"
        };
        
        foreach (var selector in confirmationSelectors)
        {
            try
            {
                var element = page.Locator(selector).First;
                if (await element.IsVisibleAsync())
                {
                    var text = await element.TextContentAsync() ?? "";
                    
                    // Extract confirmation number using regex
                    var match = Regex.Match(text, @"[A-Z0-9]{6,}", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        return match.Value;
                    }
                }
            }
            catch { }
        }
        
        return null;
    }
    
    private async Task TakeDebugScreenshot(IPage page, string name)
    {
        try
        {
            var debugPath = Path.Combine(Directory.GetCurrentDirectory(), "debug");
            Directory.CreateDirectory(debugPath);
            
            var screenshotPath = Path.Combine(debugPath, $"{DateTime.Now:yyyyMMdd_HHmmss}_{name}.png");
            await page.ScreenshotAsync(new() { Path = screenshotPath });
            
            AnsiConsole.MarkupLine($"[dim]Screenshot saved: {name}[/]");
        }
        catch { }
    }
    
    public async Task<bool> CheckAvailabilityAsync(DateTime date)
    {
        // Implementation for checking availability
        return true;
    }
}