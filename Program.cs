using Spectre.Console;
using AutoRes.Models;
using AutoRes.Services;

// Initialize settings service
var settingsService = new SettingsService();
var settings = settingsService.GetSettings();

// Display header
AnsiConsole.Clear();
var rule = new Rule("[bold blue]BC Parks Reservation Automation[/]");
rule.Justification = Justify.Center;
AnsiConsole.Write(rule);
AnsiConsole.WriteLine();

// Main menu loop
while (true)
{
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]What would you like to do?[/]")
            .PageSize(10)
            .AddChoices(new[] {
                "Make a Reservation",
                "Check Availability",
                "View Saved Credentials",
                "Exit"
            }));

    switch (choice)
    {
        case "Make a Reservation":
            await MakeReservation();
            break;
        case "Check Availability":
            await CheckAvailability();
            break;
        case "View Saved Credentials":
            ViewCredentials();
            break;
        case "Exit":
            AnsiConsole.MarkupLine("[yellow]Thank you for using BC Parks Reservation Automation![/]");
            return;
    }
    
    AnsiConsole.WriteLine();
    AnsiConsole.Prompt(new TextPrompt<string>("[grey]Press Enter to continue...[/]")
        .AllowEmpty()
        .HideChoices()
        .HideDefaultValue());
}

async Task MakeReservation()
{
    AnsiConsole.Clear();
    AnsiConsole.Write(new Rule("[bold yellow]Make a Reservation[/]"));
    
    // Select park
    var supportedParks = ParkServiceFactory.GetSupportedParks();
    supportedParks.Add("Back to Main Menu");
    
    var parkChoice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]Select a park:[/]")
            .AddChoices(supportedParks)
            .UseConverter(park => 
            {
                if (park == "Back to Main Menu") return park;
                var description = ParkServiceFactory.GetParkDescription(park);
                return $"{park}\n[dim]{description}[/]";
            }));
    
    if (parkChoice == "Back to Main Menu") return;
    
    // Check if park is supported
    if (!ParkServiceFactory.GetSupportedParks().Contains(parkChoice))
    {
        AnsiConsole.MarkupLine("[red]This park is not yet implemented. Coming soon![/]");
        return;
    }
    
    // Get reservation details
    var reservation = new ParkReservation
    {
        ParkName = parkChoice
    };
    
    // Date selection with default from last reservation
    var defaultDate = settingsService.GetDefaultDate();
    var datePrompt = new TextPrompt<DateTime>($"[green]Enter desired date (YYYY-MM-DD) [[{defaultDate:yyyy-MM-dd}]]:[/]")
        .PromptStyle("green")
        .DefaultValue(defaultDate)
        .ValidationErrorMessage("[red]Please enter a valid date in YYYY-MM-DD format[/]")
        .Validate(date =>
        {
            if (date < DateTime.Today)
                return ValidationResult.Error("[red]Date cannot be in the past[/]");
            if (date > DateTime.Today.AddDays(60))
                return ValidationResult.Error("[red]Date cannot be more than 60 days in the future[/]");
            return ValidationResult.Success();
        });
    
    reservation.DesiredDate = AnsiConsole.Prompt(datePrompt);
    
    // Number of people with default from last reservation
    var defaultPeople = settingsService.GetDefaultNumberOfPeople();
    reservation.NumberOfPeople = AnsiConsole.Prompt(
        new TextPrompt<int>($"[green]Number of people (max 4) [[{defaultPeople}]]:[/]")
            .PromptStyle("green")
            .DefaultValue(defaultPeople)
            .ValidationErrorMessage("[red]Please enter a number between 1 and 4[/]")
            .Validate(num =>
            {
                return num >= 1 && num <= 4 
                    ? ValidationResult.Success() 
                    : ValidationResult.Error("[red]Number must be between 1 and 4[/]");
            }));
    
    // Email with default from saved settings
    var emailPrompt = new TextPrompt<string>("[green]Enter your email address:[/]")
        .PromptStyle("green")
        .ValidationErrorMessage("[red]Please enter a valid email address[/]")
        .Validate(email =>
        {
            return email.Contains("@") && email.Contains(".") 
                ? ValidationResult.Success() 
                : ValidationResult.Error("[red]Invalid email format[/]");
        });
    
    if (!string.IsNullOrEmpty(settings.LastEmail))
    {
        emailPrompt.DefaultValue(settings.LastEmail);
        emailPrompt = new TextPrompt<string>($"[green]Enter your email address [[{settings.LastEmail}]]:[/]")
            .PromptStyle("green")
            .DefaultValue(settings.LastEmail)
            .ValidationErrorMessage("[red]Please enter a valid email address[/]")
            .Validate(email =>
            {
                return email.Contains("@") && email.Contains(".") 
                    ? ValidationResult.Success() 
                    : ValidationResult.Error("[red]Invalid email format[/]");
            });
    }
    
    reservation.Email = AnsiConsole.Prompt(emailPrompt);
    
    // Confirmation
    var confirm = AnsiConsole.Confirm(
        $"[yellow]Confirm reservation for {reservation.NumberOfPeople} people at {parkChoice} on {reservation.DesiredDate:yyyy-MM-dd}?[/]");
    
    if (!confirm)
    {
        AnsiConsole.MarkupLine("[red]Reservation cancelled.[/]");
        return;
    }
    
    // Make the reservation with smart self-healing service
    var service = ParkServiceFactory.CreateService(parkChoice);
    
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[bold]Starting reservation process...[/]");
    AnsiConsole.MarkupLine("[dim]Note: A browser window will open. Please do not close it.[/]");
    AnsiConsole.WriteLine();
    
    var result = await service.MakeReservationAsync(reservation);
    
    AnsiConsole.WriteLine();
    
    if (result.Success)
    {
        // Save the successful reservation details
        settingsService.SaveLastReservation(parkChoice, reservation.DesiredDate, reservation.NumberOfPeople, reservation.Email);
        
        var panel = new Panel(
            $"[bold green]Reservation Successful![/]\n\n" +
            $"[yellow]Confirmation Number:[/] {result.ConfirmationNumber ?? "N/A"}\n" +
            $"[yellow]Date:[/] {result.ReservationDate:yyyy-MM-dd}\n" +
            $"[yellow]Park:[/] {parkChoice}\n" +
            $"[yellow]People:[/] {reservation.NumberOfPeople}")
        {
            Header = new PanelHeader("[bold green]Success[/]"),
            Border = BoxBorder.Double,
            Padding = new Padding(2, 1)
        };
        
        AnsiConsole.Write(panel);
        AnsiConsole.MarkupLine("\n[dim]A screenshot of your confirmation has been saved.[/]");
    }
    else
    {
        AnsiConsole.MarkupLine($"[bold red]Reservation Failed[/]");
        AnsiConsole.MarkupLine($"[red]{result.ErrorMessage}[/]");
    }
}

async Task CheckAvailability()
{
    AnsiConsole.Clear();
    AnsiConsole.Write(new Rule("[bold yellow]Check Availability[/]"));
    
    var supportedParks = ParkServiceFactory.GetSupportedParks();
    supportedParks.Add("Back to Main Menu");
    
    var parkChoice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[green]Select a park:[/]")
            .AddChoices(supportedParks));
    
    if (parkChoice == "Back to Main Menu") return;
    
    var date = AnsiConsole.Prompt(
        new TextPrompt<DateTime>("[green]Enter date to check (YYYY-MM-DD):[/]")
            .PromptStyle("green"));
    
    var service = ParkServiceFactory.CreateService(parkChoice);
    
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("[yellow]Checking availability...[/]", async ctx =>
        {
            var isAvailable = await service.CheckAvailabilityAsync(date);
            
            if (isAvailable)
            {
                AnsiConsole.MarkupLine($"[green]✓ Slots available for {date:yyyy-MM-dd}[/]");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ No slots available for {date:yyyy-MM-dd}[/]");
            }
        });
}

void ViewCredentials()
{
    AnsiConsole.Clear();
    AnsiConsole.Write(new Rule("[bold yellow]Saved Settings[/]"));
    
    var currentSettings = settingsService.GetSettings();
    
    var table = new Table();
    table.AddColumn("Setting");
    table.AddColumn("Value");
    
    table.AddRow("Email", currentSettings.LastEmail ?? "[grey]Not set[/]");
    table.AddRow("Last Park", currentSettings.LastSelectedPark ?? "[grey]Not set[/]");
    table.AddRow("Last Date", currentSettings.LastSelectedDate?.ToString("yyyy-MM-dd") ?? "[grey]Not set[/]");
    table.AddRow("Last Party Size", currentSettings.LastNumberOfPeople?.ToString() ?? "[grey]Not set[/]");
    
    AnsiConsole.Write(table);
    
    if (!string.IsNullOrEmpty(currentSettings.LastEmail))
    {
        AnsiConsole.WriteLine();
        var clearData = AnsiConsole.Confirm("[yellow]Do you want to clear saved settings?[/]");
        if (clearData)
        {
            settingsService.SaveLastReservation("", DateTime.MinValue, 0, "");
            AnsiConsole.MarkupLine("[green]Settings cleared![/]");
        }
    }
}