using Microsoft.Playwright;
using AutoRes.Models;
using Spectre.Console;
using System.Text.RegularExpressions;

namespace AutoRes.Services;

public class SmartGaribaldiReservationService : IParkReservationService
{
    public string ParkName => "Garibaldi Provincial Park";
    
    private const string ReservationUrl = "https://reserve.bcparks.ca/dayuse/";
    private readonly AutomationLearningService _learningService;
    
    // Garibaldi has three main trailheads
    public enum Trailhead
    {
        DiamondHead,
        RubbleCreek,
        CheakamusLake
    }
    
    public class ParkStatus
    {
        public bool IsClosed { get; set; }
        public string ClosureReason { get; set; } = string.Empty;
        public bool IsAvailable => !IsClosed;
    }
    
    public SmartGaribaldiReservationService()
    {
        _learningService = new AutomationLearningService(ParkName);
    }
    
    public async Task<ReservationResult> MakeReservationAsync(ParkReservation reservation)
    {
        var result = new ReservationResult();
        _learningService.StartSession(ParkName);
        
        // Ask for trailhead preference
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
                var task = ctx.AddTask($"[green]Making smart reservation for Garibaldi - {trailhead}[/]");
                
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
                    
                    task.Description = "[yellow]Searching for Garibaldi Park[/]";
                    task.Increment(10);
                    
                    // Check if Garibaldi is currently available for booking
                    var parkStatus = await CheckParkStatus(page);
                    
                    if (parkStatus.IsClosed)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Garibaldi Provincial Park is currently closed for reservations. {parkStatus.ClosureReason}";
                        _learningService.EndSession(false, result.ErrorMessage);
                        return;
                    }
                    
                    // Try multiple strategies to find the park
                    var parkFound = await TryFindAndSelectPark(page, trailhead);
                    
                    if (!parkFound)
                    {
                        throw new Exception($"Could not find Garibaldi Park - {trailhead} in the park list");
                    }
                    
                    await TakeDebugScreenshot(page, "02_park_selected");
                    
                    task.Description = "[yellow]Selecting date[/]";
                    task.Increment(20);
                    
                    // Check if date requires a pass
                    var requiresPass = await CheckIfDateRequiresPass(reservation.DesiredDate, trailhead);
                    if (!requiresPass)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Day passes are not required for {trailhead} on {reservation.DesiredDate:yyyy-MM-dd}. You can visit without a reservation!";
                        _learningService.EndSession(false, result.ErrorMessage);
                        return;
                    }
                    
                    var dateSelected = await TrySelectDate(page, reservation.DesiredDate);
                    
                    if (!dateSelected)
                    {
                        throw new Exception("Could not select the desired date");
                    }
                    
                    await TakeDebugScreenshot(page, "03_date_selected");
                    
                    // For Garibaldi, we need vehicle info instead of number of people
                    task.Description = "[yellow]Selecting vehicle pass[/]";
                    task.Increment(10);
                    
                    var vehicleSelected = await TrySelectVehiclePass(page);
                    
                    if (!vehicleSelected)
                    {
                        throw new Exception("Could not select vehicle pass");
                    }
                    
                    await TakeDebugScreenshot(page, "04_vehicle_selected");
                    
                    task.Description = "[yellow]Checking availability[/]";
                    task.Increment(20);
                    
                    var availabilityChecked = await TryCheckAvailability(page);
                    
                    if (!availabilityChecked)
                    {
                        throw new Exception("Could not check availability");
                    }
                    
                    await TakeDebugScreenshot(page, "05_availability_checked");
                    
                    task.Description = "[yellow]Selecting available pass[/]";
                    task.Increment(10);
                    
                    var passSelected = await TrySelectAvailablePass(page);
                    
                    if (!passSelected)
                    {
                        result.ErrorMessage = $"No available passes found for {trailhead} on the selected date.";
                        _learningService.EndSession(false, result.ErrorMessage);
                        return;
                    }
                    
                    await TakeDebugScreenshot(page, "06_pass_selected");
                    
                    task.Description = "[yellow]Entering contact information[/]";
                    task.Increment(10);
                    
                    var contactEntered = await TryEnterContactInfo(page, reservation.Email);
                    
                    if (!contactEntered)
                    {
                        throw new Exception("Could not enter contact information");
                    }
                    
                    // Garibaldi might also ask for vehicle information
                    await TryEnterVehicleInfo(page);
                    
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
                        await page.ScreenshotAsync(new() { Path = $"garibaldi_confirmation_{DateTime.Now:yyyyMMdd_HHmmss}.png" });
                        
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
                        using var playwright = await Playwright.CreateAsync();
                        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
                        var page = await browser.NewPageAsync();
                        await TakeDebugScreenshot(page, "error_state");
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
    
    private async Task<Trailhead> SelectTrailhead()
    {
        var trailhead = AnsiConsole.Prompt(
            new SelectionPrompt<Trailhead>()
                .Title("[green]Select Garibaldi trailhead:[/]")
                .AddChoices(new[] {
                    Trailhead.DiamondHead,
                    Trailhead.RubbleCreek,
                    Trailhead.CheakamusLake
                }));
        
        return await Task.FromResult(trailhead);
    }
    
    private async Task<ParkStatus> CheckParkStatus(IPage page)
    {
        _learningService.RecordStep("CheckParkStatus", "check_availability");
        
        try
        {
            // Look for Garibaldi park section and check its status
            var garibaldiSection = page.Locator("text=/Garibaldi Provincial Park/i").First;
            
            if (await garibaldiSection.IsVisibleAsync())
            {
                // Get the parent container of the Garibaldi section
                var parkContainer = garibaldiSection.Locator("xpath=ancestor::div[contains(@class, 'park') or contains(@class, 'card') or contains(@class, 'section')][1]");
                
                // Check for closure indicators
                var closureIndicators = new[]
                {
                    "text=/closed/i",
                    "[class*='closed']",
                    "[class*='unavailable']",
                    ".status-closed",
                    "text=/temporarily closed/i"
                };
                
                foreach (var indicator in closureIndicators)
                {
                    var closureElement = parkContainer.Locator(indicator).First;
                    if (await closureElement.IsVisibleAsync())
                    {
                        // Try to get the closure reason
                        var reasonText = await closureElement.TextContentAsync() ?? "";
                        
                        // Look for additional closure information nearby
                        var additionalInfo = parkContainer.Locator("text=/rubble creek/i, text=/access points/i").First;
                        if (await additionalInfo.IsVisibleAsync())
                        {
                            var additionalText = await additionalInfo.TextContentAsync() ?? "";
                            reasonText += " " + additionalText;
                        }
                        
                        _learningService.RecordSuccess("CheckParkStatus");
                        return new ParkStatus 
                        { 
                            IsClosed = true, 
                            ClosureReason = reasonText.Trim() 
                        };
                    }
                }
                
                // Check if there's a "Book a Pass" button available
                var bookButton = parkContainer.Locator("button:has-text('Book a Pass'), a:has-text('Book a Pass')").First;
                if (!await bookButton.IsVisibleAsync())
                {
                    _learningService.RecordSuccess("CheckParkStatus");
                    return new ParkStatus 
                    { 
                        IsClosed = true, 
                        ClosureReason = "No booking button available - park may be closed or unavailable" 
                    };
                }
            }
            
            _learningService.RecordSuccess("CheckParkStatus");
            return new ParkStatus { IsClosed = false };
        }
        catch (Exception ex)
        {
            _learningService.RecordError("CheckParkStatus", ex.Message);
            // If we can't determine status, assume it's available and let the booking process handle any issues
            return new ParkStatus { IsClosed = false };
        }
    }
    
    private async Task<bool> CheckIfDateRequiresPass(DateTime date, Trailhead trailhead)
    {
        // Check if the date requires a pass based on the rules
        if (trailhead == Trailhead.CheakamusLake)
        {
            // April 25 - September 1: Daily
            if (date >= new DateTime(date.Year, 4, 25) && date <= new DateTime(date.Year, 9, 1))
                return true;
            
            // September 2 - October 13: Fri, Sat, Sun, Mon, Holidays
            if (date >= new DateTime(date.Year, 9, 2) && date <= new DateTime(date.Year, 10, 13))
            {
                return date.DayOfWeek == DayOfWeek.Friday || 
                       date.DayOfWeek == DayOfWeek.Saturday || 
                       date.DayOfWeek == DayOfWeek.Sunday || 
                       date.DayOfWeek == DayOfWeek.Monday;
            }
        }
        else // Diamond Head and Rubble Creek
        {
            // June 13 - October 13: Fri, Sat, Sun, Mon, Holidays
            if (date >= new DateTime(date.Year, 6, 13) && date <= new DateTime(date.Year, 10, 13))
            {
                return date.DayOfWeek == DayOfWeek.Friday || 
                       date.DayOfWeek == DayOfWeek.Saturday || 
                       date.DayOfWeek == DayOfWeek.Sunday || 
                       date.DayOfWeek == DayOfWeek.Monday;
            }
        }
        
        return await Task.FromResult(false);
    }
    
    private async Task<bool> TryFindAndSelectPark(IPage page, Trailhead trailhead)
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
        
        // Try searching for "Garibaldi"
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
                    await element.FillAsync("Garibaldi");
                    await page.Keyboard.PressAsync("Enter");
                    await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
                    
                    _learningService.AddSelector("FindPark", selector.Type, selector.Value, true);
                    _learningService.RecordSuccess("FindPark");
                    
                    // Now try to click on the specific trailhead
                    return await TryClickParkFromResults(page, trailhead);
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
                var garibaldiOption = options.FirstOrDefault(o => 
                    o.Contains("Garibaldi", StringComparison.OrdinalIgnoreCase) &&
                    o.Contains(trailhead.ToString(), StringComparison.OrdinalIgnoreCase));
                
                if (garibaldiOption != null)
                {
                    await parkSelect.SelectOptionAsync(new[] { garibaldiOption });
                    _learningService.AddSelector("FindPark", "css", "select", true);
                    _learningService.RecordSuccess("FindPark");
                    return true;
                }
            }
        }
        catch { }
        
        // Strategy 3: Direct link clicking
        return await TryClickParkFromResults(page, trailhead);
    }
    
    private async Task<bool> TryClickParkFromResults(IPage page, Trailhead trailhead)
    {
        var trailheadName = trailhead switch
        {
            Trailhead.DiamondHead => "Diamond Head",
            Trailhead.RubbleCreek => "Rubble Creek",
            Trailhead.CheakamusLake => "Cheakamus Lake",
            _ => trailhead.ToString()
        };
        
        var linkSelectors = new[]
        {
            $"text=/Garibaldi.*{trailheadName}/i",
            $"text=/{trailheadName}/i",
            $"[href*='garibaldi'][href*='{trailheadName.ToLower().Replace(" ", "-")}']",
            $"a:has-text('Garibaldi'):has-text('{trailheadName}')",
            $"button:has-text('Garibaldi'):has-text('{trailheadName}')",
            "text=/Garibaldi/i" // Fallback to just Garibaldi
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
    
    private async Task<bool> TrySelectVehiclePass(IPage page)
    {
        _learningService.RecordStep("SelectVehicle", "select");
        
        // For Garibaldi, we typically need to select vehicle type or number
        var vehicleSelectors = new[]
        {
            "select[name*='vehicle']",
            "input[type='radio'][value='vehicle']",
            "label:has-text('Vehicle')",
            "button:has-text('Vehicle Pass')",
            "#vehicle-type",
            "[aria-label*='vehicle']"
        };
        
        foreach (var selector in vehicleSelectors)
        {
            try
            {
                var element = page.Locator(selector).First;
                if (await element.IsVisibleAsync())
                {
                    if (selector.Contains("select"))
                    {
                        await element.SelectOptionAsync("1"); // Select 1 vehicle
                    }
                    else
                    {
                        await element.ClickAsync();
                    }
                    
                    _learningService.AddSelector("SelectVehicle", "css", selector, true);
                    _learningService.RecordSuccess("SelectVehicle");
                    return true;
                }
            }
            catch { }
        }
        
        // If no specific vehicle selector, might be included in general flow
        return true;
    }
    
    private async Task<bool> TrySelectAvailablePass(IPage page)
    {
        _learningService.RecordStep("SelectPass", "click");
        
        // Look for available passes (different from time slots)
        var passSelectors = new[]
        {
            ".pass-available:not(.disabled)",
            ".vehicle-pass:not(.unavailable)",
            "[class*='pass']:not([class*='disabled'])",
            "button[class*='available'][class*='pass']",
            "[data-available='true'][data-type='vehicle']",
            ".availability-item.available"
        };
        
        foreach (var selector in passSelectors)
        {
            try
            {
                var elements = await page.Locator(selector).AllAsync();
                if (elements.Count > 0)
                {
                    await elements[0].ClickAsync();
                    await page.WaitForTimeoutAsync(500);
                    
                    _learningService.AddSelector("SelectPass", "css", selector, true);
                    _learningService.RecordSuccess("SelectPass");
                    
                    // Click continue/next button
                    await TryClickContinue(page);
                    
                    return true;
                }
            }
            catch { }
        }
        
        return false;
    }
    
    private async Task TryEnterVehicleInfo(IPage page)
    {
        try
        {
            // Look for vehicle info fields
            var licensePlateSelectors = new[]
            {
                "input[name*='license']",
                "input[name*='plate']",
                "input[placeholder*='license']",
                "#license-plate"
            };
            
            foreach (var selector in licensePlateSelectors)
            {
                try
                {
                    var element = page.Locator(selector).First;
                    if (await element.IsVisibleAsync())
                    {
                        // Use a placeholder license plate
                        await element.FillAsync("ABC123");
                        _learningService.AddSelector("VehicleInfo", "css", selector, true);
                        break;
                    }
                }
                catch { }
            }
            
            // Look for vehicle make/model
            var vehicleMakeSelectors = new[]
            {
                "input[name*='make']",
                "input[name*='vehicle']",
                "select[name*='make']"
            };
            
            foreach (var selector in vehicleMakeSelectors)
            {
                try
                {
                    var element = page.Locator(selector).First;
                    if (await element.IsVisibleAsync())
                    {
                        if (selector.Contains("select"))
                        {
                            await element.SelectOptionAsync("Other");
                        }
                        else
                        {
                            await element.FillAsync("Vehicle");
                        }
                        break;
                    }
                }
                catch { }
            }
        }
        catch { }
    }
    
    private async Task<bool> TrySelectDate(IPage page, DateTime date)
    {
        _learningService.RecordStep("SelectDate", "date_selection", date.ToString("yyyy-MM-dd"));
        
        // Similar to Joffre Lakes implementation
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
        
        // Try calendar picker if date input doesn't work
        try
        {
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
                        
                        var dateString = date.Day.ToString();
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
                    
                    await page.WaitForSelectorAsync("[class*='availability'], [class*='pass'], [class*='vehicle']", 
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
        // Simple implementation - in reality would check actual availability
        return await Task.FromResult(true);
    }
}