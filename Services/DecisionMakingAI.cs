using Microsoft.Playwright;
using Spectre.Console;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AutoRes.Services;

/// <summary>
/// AI system focused on decision-making and content analysis for park reservation automation
/// Analyzes page content to understand rules, availability, and form requirements
/// </summary>
public class DecisionMakingAI
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _model;
    private IPage? _page;
    
    public DecisionMakingAI(string model = "gpt-4o-mini")
    {
        _httpClient = new HttpClient();
        _apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        _model = model;
        
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
            AnsiConsole.MarkupLine($"[green]🧠 Decision-making AI initialized with {_model}[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]⚠️ No OpenAI key - using rule-based decisions[/]");
        }
    }
    
    public void SetPage(IPage page)
    {
        _page = page;
    }
    
    /// <summary>
    /// Analyzes page content to understand booking rules and requirements
    /// </summary>
    public async Task<BookingRules> AnalyzeBookingRules()
    {
        if (_page == null) return new BookingRules();
        
        try
        {
            var pageText = await _page.Locator("body").TextContentAsync();
            
            if (!string.IsNullOrEmpty(_apiKey))
            {
                return await AnalyzeRulesWithAI(pageText);
            }
            else
            {
                return AnalyzeRulesWithPatterns(pageText);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error analyzing rules: {ex.Message}[/]");
            return new BookingRules();
        }
    }
    
    /// <summary>
    /// Determines if a specific date is available based on page content
    /// </summary>
    public async Task<DateAvailabilityResult> CheckDateAvailability(DateTime targetDate)
    {
        if (_page == null) return new DateAvailabilityResult { IsAvailable = false, Reason = "No page set" };
        
        try
        {
            var pageText = await _page.Locator("body").TextContentAsync();
            
            if (!string.IsNullOrEmpty(_apiKey))
            {
                return await CheckDateWithAI(pageText, targetDate);
            }
            else
            {
                return CheckDateWithPatterns(pageText, targetDate);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error checking date: {ex.Message}[/]");
            return new DateAvailabilityResult { IsAvailable = false, Reason = ex.Message };
        }
    }
    
    /// <summary>
    /// Analyzes form fields and determines how to fill them
    /// </summary>
    public async Task<FormAnalysis> AnalyzeForms()
    {
        if (_page == null) return new FormAnalysis();
        
        try
        {
            var formElements = await GetFormElements();
            
            if (!string.IsNullOrEmpty(_apiKey))
            {
                return await AnalyzeFormsWithAI(formElements);
            }
            else
            {
                return AnalyzeFormsWithPatterns(formElements);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error analyzing forms: {ex.Message}[/]");
            return new FormAnalysis();
        }
    }
    
    /// <summary>
    /// Makes decisions when the booking process encounters errors or unexpected states
    /// </summary>
    public async Task<ProcessDecision> HandleUnexpectedState(string situation)
    {
        if (!string.IsNullOrEmpty(_apiKey))
        {
            return await HandleStateWithAI(situation);
        }
        else
        {
            return HandleStateWithRules(situation);
        }
    }
    
    private async Task<BookingRules> AnalyzeRulesWithAI(string pageText)
    {
        var prompt = $@"Analyze this BC Parks reservation page content and extract booking rules:

{pageText.Substring(0, Math.Min(pageText.Length, 2000))}

Extract these specific details in JSON format:
{{
  ""bookingStartTime"": ""time when booking opens (e.g., 7:00am)"",
  ""advanceBookingDays"": ""how many days in advance (e.g., 2)"",
  ""timeLimit"": ""time limit to complete booking (e.g., 7 minutes)"",
  ""passTypes"": [""list of pass types available""],
  ""maxPassesPerDay"": ""how many passes per person per day"",
  ""transferable"": ""whether passes can be transferred"",
  ""cancellationPolicy"": ""when cancelled passes become available""
}}

Focus on actionable booking information.";

        var response = await CallOpenAI(prompt);
        
        try
        {
            return JsonSerializer.Deserialize<BookingRules>(response) ?? new BookingRules();
        }
        catch
        {
            return ParseRulesFromText(response);
        }
    }
    
    private BookingRules AnalyzeRulesWithPatterns(string pageText)
    {
        var rules = new BookingRules();
        
        // Pattern matching for common rules
        if (Regex.IsMatch(pageText, @"7:?00\s*am|7am", RegexOptions.IgnoreCase))
        {
            rules.BookingStartTime = "7:00am";
        }
        
        if (Regex.IsMatch(pageText, @"two days? before|2 days? before", RegexOptions.IgnoreCase))
        {
            rules.AdvanceBookingDays = 2;
        }
        
        if (Regex.IsMatch(pageText, @"7 minutes?|seven minutes?", RegexOptions.IgnoreCase))
        {
            rules.TimeLimit = "7 minutes";
        }
        
        if (Regex.IsMatch(pageText, @"AM.*PM.*ALL DAY|ALL DAY.*AM.*PM", RegexOptions.IgnoreCase))
        {
            rules.PassTypes = new[] { "AM", "PM", "ALL DAY" };
        }
        
        if (Regex.IsMatch(pageText, @"cannot transfer|not transferable", RegexOptions.IgnoreCase))
        {
            rules.Transferable = false;
        }
        
        return rules;
    }
    
    private async Task<DateAvailabilityResult> CheckDateWithAI(string pageText, DateTime targetDate)
    {
        var prompt = $@"Analyze this BC Parks reservation page for booking availability:

Target Date: {targetDate:yyyy-MM-dd}
Current Date: {DateTime.Now:yyyy-MM-dd}

Page Content:
{pageText.Substring(0, Math.Min(pageText.Length, 1500))}

CRITICAL ANALYSIS NEEDED:
This page may show contradictory information like 'Garibaldi Provincial Park is currently closed for reservations' 
BUT also have a 'Book a Pass' button visible. This is common in BC Parks system.

DECISION RULES:
1. If page shows 'closed' BUT has booking buttons/links → AVAILABLE (proceed with booking)
2. If page shows 'closed' AND no booking options → NOT AVAILABLE  
3. Look for: 'Book a Pass', 'Book Now', 'Reserve', 'Select' buttons
4. Ignore general closure messages if specific booking options exist

Respond in JSON format with these fields:
- isAvailable: true/false
- reason: explanation of decision logic  
- recommendedAction: specific next step
- bookingButtonsFound: list any booking buttons/links found
- contradictionDetected: true/false";

        var response = await CallOpenAI(prompt);
        
        try
        {
            return JsonSerializer.Deserialize<DateAvailabilityResult>(response) ?? new DateAvailabilityResult();
        }
        catch
        {
            return ParseDateResultFromText(response);
        }
    }
    
    private DateAvailabilityResult CheckDateWithPatterns(string pageText, DateTime targetDate)
    {
        var result = new DateAvailabilityResult();
        var pageUpper = pageText.ToUpper();
        
        // CRITICAL: Handle "closed but has booking options" scenario
        bool hasClosed = pageUpper.Contains("CLOSED") || pageUpper.Contains("CURRENTLY CLOSED");
        bool hasBookingOptions = pageUpper.Contains("BOOK A PASS") || pageUpper.Contains("BOOK NOW") || 
                               pageUpper.Contains("RESERVE") || pageUpper.Contains("SELECT");
        
        if (hasClosed && hasBookingOptions)
        {
            result.IsAvailable = true;
            result.Reason = "Park shows as 'closed' but booking options are available - proceeding with booking";
            result.RecommendedAction = "Click 'Book a Pass' button - ignore general closure status";
            AnsiConsole.MarkupLine("[green]🧠 AI Decision: Found contradiction - 'closed' status but booking button present. Proceeding![/]");
            return result;
        }
        
        // Check if date is too early (before booking window)
        var bookingDays = 2; // Default from common pattern
        var earliestBookingDate = DateTime.Now.AddDays(bookingDays);
        
        if (targetDate < earliestBookingDate)
        {
            result.IsAvailable = false;
            result.Reason = $"Date is too early. Booking opens {bookingDays} days in advance.";
            result.RecommendedAction = $"Wait until {earliestBookingDate.AddDays(-bookingDays):yyyy-MM-dd} at 7:00am";
        }
        else if (pageUpper.Contains("SOLD OUT") || pageUpper.Contains("NO PASSES AVAILABLE"))
        {
            result.IsAvailable = false;
            result.Reason = "All passes sold out for this date";
            result.RecommendedAction = "Check for cancellations or try alternative dates";
        }
        else if (pageUpper.Contains("AVAILABLE") || pageUpper.Contains("BOOK NOW") || hasBookingOptions)
        {
            result.IsAvailable = true;
            result.Reason = "Passes appear to be available";
            result.RecommendedAction = "Proceed with booking";
        }
        else if (hasClosed && !hasBookingOptions)
        {
            result.IsAvailable = false;
            result.Reason = "Park is closed and no booking options found";
            result.RecommendedAction = "Check park status and reopening dates";
        }
        else
        {
            result.IsAvailable = false;
            result.Reason = "Unable to determine availability";
            result.RecommendedAction = "Refresh page and check again";
        }
        
        return result;
    }
    
    private async Task<FormAnalysis> AnalyzeFormsWithAI(Dictionary<string, FormElement> formElements)
    {
        var formData = JsonSerializer.Serialize(formElements, new JsonSerializerOptions { WriteIndented = true });
        
        var prompt = $@"Analyze these form elements for a BC Parks reservation:

{formData}

Determine the optimal filling strategy:
{{
  ""fillOrder"": [""ordered list of field names to fill""],
  ""fieldMappings"": {{
    ""date"": ""which field is for date"",
    ""people"": ""which field is for number of people"",
    ""email"": ""which field is for email"",
    ""passType"": ""which field is for pass type selection""
  }},
  ""requiredFields"": [""list of required fields""],
  ""submitButton"": ""name or id of submit button"",
  ""warnings"": [""any warnings or special requirements""]
}}";

        var response = await CallOpenAI(prompt);
        
        try
        {
            return JsonSerializer.Deserialize<FormAnalysis>(response) ?? new FormAnalysis();
        }
        catch
        {
            return AnalyzeFormsWithPatterns(formElements);
        }
    }
    
    private FormAnalysis AnalyzeFormsWithPatterns(Dictionary<string, FormElement> formElements)
    {
        var analysis = new FormAnalysis();
        
        foreach (var element in formElements)
        {
            var name = element.Key.ToLower();
            var elementInfo = element.Value;
            
            if (name.Contains("date") || name.Contains("calendar"))
            {
                analysis.FieldMappings["date"] = element.Key;
            }
            else if (name.Contains("people") || name.Contains("party") || name.Contains("size"))
            {
                analysis.FieldMappings["people"] = element.Key;
            }
            else if (name.Contains("email") || name.Contains("contact"))
            {
                analysis.FieldMappings["email"] = element.Key;
            }
            else if (name.Contains("pass") || name.Contains("type") || name.Contains("time"))
            {
                analysis.FieldMappings["passType"] = element.Key;
            }
            
            if (elementInfo.Required)
            {
                analysis.RequiredFields.Add(element.Key);
            }
            
            if (elementInfo.Type == "submit" || elementInfo.Type == "button")
            {
                analysis.SubmitButton = element.Key;
            }
        }
        
        // Set logical fill order
        analysis.FillOrder = new List<string>
        {
            analysis.FieldMappings.GetValueOrDefault("passType", ""),
            analysis.FieldMappings.GetValueOrDefault("date", ""),
            analysis.FieldMappings.GetValueOrDefault("people", ""),
            analysis.FieldMappings.GetValueOrDefault("email", "")
        }.Where(x => !string.IsNullOrEmpty(x)).ToList();
        
        return analysis;
    }
    
    private ProcessDecision HandleStateWithRules(string situation)
    {
        var situationUpper = situation.ToUpper();
        
        if (situationUpper.Contains("SOLD OUT") || situationUpper.Contains("NO AVAILABILITY"))
        {
            return new ProcessDecision
            {
                Action = "WAIT_AND_RETRY",
                Reason = "Passes sold out - check for cancellations",
                WaitTime = TimeSpan.FromMinutes(5),
                ShouldRetry = true
            };
        }
        
        if (situationUpper.Contains("TOO EARLY") || situationUpper.Contains("NOT YET AVAILABLE"))
        {
            return new ProcessDecision
            {
                Action = "WAIT_FOR_BOOKING_WINDOW",
                Reason = "Booking window not yet open",
                WaitTime = TimeSpan.FromHours(1),
                ShouldRetry = true
            };
        }
        
        if (situationUpper.Contains("EXPIRED") || situationUpper.Contains("TIMEOUT"))
        {
            return new ProcessDecision
            {
                Action = "RESTART_BOOKING",
                Reason = "Session expired - restart booking process",
                WaitTime = TimeSpan.FromSeconds(10),
                ShouldRetry = true
            };
        }
        
        return new ProcessDecision
        {
            Action = "CONTINUE",
            Reason = "No specific issue detected",
            ShouldRetry = false
        };
    }
    
    private async Task<ProcessDecision> HandleStateWithAI(string situation)
    {
        var prompt = $@"A BC Parks reservation process encountered this situation: {situation}

Based on typical booking system behavior, what should the automation do?

Respond in JSON:
{{
  ""action"": ""CONTINUE|WAIT_AND_RETRY|RESTART_BOOKING|ABORT|REFRESH_PAGE"",
  ""reason"": ""explanation for this decision"",
  ""waitTimeMinutes"": ""how long to wait if applicable"",
  ""shouldRetry"": ""true/false whether to retry the operation"",
  ""specificSteps"": [""list of specific actions to take""]
}}";

        var response = await CallOpenAI(prompt);
        
        try
        {
            var result = JsonSerializer.Deserialize<ProcessDecision>(response);
            return result ?? HandleStateWithRules(situation);
        }
        catch
        {
            return HandleStateWithRules(situation);
        }
    }
    
    private async Task<string> CallOpenAI(string prompt)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "You are an expert at analyzing BC Parks reservation systems. Provide concise, actionable analysis in the requested format." },
                new { role = "user", content = prompt }
            },
            max_tokens = 1000,
            temperature = 0.1
        };
        
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        
        var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
        
        if (response.IsSuccessStatusCode)
        {
            var responseJson = await response.Content.ReadAsStringAsync();
            var responseObj = JsonSerializer.Deserialize<JsonElement>(responseJson);
            
            if (responseObj.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var firstChoice = choices[0];
                if (firstChoice.TryGetProperty("message", out var message) && 
                    message.TryGetProperty("content", out var contentProp))
                {
                    return contentProp.GetString() ?? "";
                }
            }
        }
        
        throw new Exception($"OpenAI API call failed: {response.StatusCode}");
    }
    
    private async Task<Dictionary<string, FormElement>> GetFormElements()
    {
        var elements = new Dictionary<string, FormElement>();
        
        if (_page == null) return elements;
        
        // Get all form inputs
        var inputs = await _page.Locator("input, select, textarea, button").AllAsync();
        
        foreach (var input in inputs)
        {
            try
            {
                var name = await input.GetAttributeAsync("name") ?? await input.GetAttributeAsync("id") ?? "";
                var type = await input.GetAttributeAsync("type") ?? "text";
                var required = await input.GetAttributeAsync("required") != null;
                var placeholder = await input.GetAttributeAsync("placeholder") ?? "";
                
                if (!string.IsNullOrEmpty(name))
                {
                    elements[name] = new FormElement
                    {
                        Name = name,
                        Type = type,
                        Required = required,
                        Placeholder = placeholder
                    };
                }
            }
            catch { }
        }
        
        return elements;
    }
    
    private BookingRules ParseRulesFromText(string text)
    {
        // Fallback parsing when JSON fails
        return AnalyzeRulesWithPatterns(text);
    }
    
    private DateAvailabilityResult ParseDateResultFromText(string text)
    {
        var result = new DateAvailabilityResult();
        var textUpper = text.ToUpper();
        
        result.IsAvailable = textUpper.Contains("AVAILABLE") && !textUpper.Contains("NOT AVAILABLE");
        result.Reason = text.Length > 200 ? text.Substring(0, 200) + "..." : text;
        
        return result;
    }
    
    /// <summary>
    /// Analyzes a failure to determine if it's valid or if the process can be healed
    /// </summary>
    public async Task<FailureAnalysis> AnalyzeFailureAndSuggestHealing(string failureMessage, IPage? page = null)
    {
        try
        {
            string pageContent = "";
            if (page != null)
            {
                _page = page;
                pageContent = await _page.Locator("body").TextContentAsync() ?? "";
            }
            
            if (!string.IsNullOrEmpty(_apiKey))
            {
                return await AnalyzeFailureWithAI(failureMessage, pageContent);
            }
            else
            {
                return AnalyzeFailureWithPatterns(failureMessage, pageContent);
            }
        }
        catch (Exception ex)
        {
            return new FailureAnalysis
            {
                IsFailureValid = true,
                Reason = $"Unable to analyze failure: {ex.Message}",
                HealingInstructions = new List<string> { "Manual intervention required" }
            };
        }
    }
    
    private async Task<FailureAnalysis> AnalyzeFailureWithAI(string failureMessage, string pageContent)
    {
        var prompt = $@"FAILURE ANALYSIS AND HEALING for BC Parks Reservation System:

REPORTED FAILURE: {failureMessage}

CURRENT PAGE STATE:
{pageContent}

ANALYSIS TASK:
The automation reported a failure, but we need to verify if this failure is actually correct or if there are booking options available that were missed.

SPECIFIC SCENARIO:
- Failure says: 'Garibaldi Provincial Park is currently closed for reservations'
- But user reports: 'I can see there are some options to select' or 'I can see the book a pass button'

CRITICAL QUESTIONS:
1. Is the reported failure actually correct, or are there hidden booking opportunities?
2. What specific interactive elements (buttons, links, forms) are visible that could lead to successful booking?
3. What exact steps should the automation take to overcome this failure?
4. Are there alternative paths or workflows available?

HEALING FOCUS:
- Look for 'Book a Pass', 'Book Now', 'Reserve', 'Select' buttons
- Identify specific park sections or trailheads that might be bookable
- Find date selectors, forms, or booking flows that were missed
- Provide step-by-step healing instructions

Respond in JSON format:
{{
  ""isFailureValid"": true/false,
  ""reason"": ""detailed explanation of why failure is valid or invalid"",
  ""bookingOptionsFound"": [""list of specific booking elements discovered""],
  ""healingInstructions"": [""step-by-step instructions to fix the process""],
  ""confidenceLevel"": ""HIGH/MEDIUM/LOW"",
  ""alternativeStrategies"": [""backup approaches if main healing fails""]
}}";

        var response = await CallOpenAI(prompt);
        
        try
        {
            return JsonSerializer.Deserialize<FailureAnalysis>(response) ?? new FailureAnalysis();
        }
        catch
        {
            return ParseFailureAnalysisFromText(response, failureMessage, pageContent);
        }
    }
    
    private FailureAnalysis AnalyzeFailureWithPatterns(string failureMessage, string pageContent)
    {
        var analysis = new FailureAnalysis();
        var failureUpper = failureMessage.ToUpper();
        var pageUpper = pageContent.ToUpper();
        
        // Pattern-based failure analysis
        if (failureUpper.Contains("CLOSED") && failureUpper.Contains("RESERVATIONS"))
        {
            // Check if page actually has booking options despite "closed" message
            bool hasBookingElements = pageUpper.Contains("BOOK A PASS") || 
                                    pageUpper.Contains("BOOK NOW") || 
                                    pageUpper.Contains("RESERVE") || 
                                    pageUpper.Contains("SELECT") ||
                                    pageUpper.Contains("CHOOSE");
            
            if (hasBookingElements)
            {
                analysis.IsFailureValid = false;
                analysis.Reason = "Failure is INCORRECT - Page shows 'closed' but booking options are available";
                analysis.BookingOptionsFound = ExtractBookingElements(pageContent);
                analysis.HealingInstructions = new List<string>
                {
                    "1. Ignore the general 'closed' status message",
                    "2. Look for and click 'Book a Pass' or similar booking buttons",
                    "3. Search for specific trailhead or area booking options",
                    "4. Try clicking on park sections that may have individual availability",
                    "5. Look for date selectors or booking forms that may be present"
                };
                analysis.ConfidenceLevel = "HIGH";
                analysis.AlternativeStrategies = new List<string>
                {
                    "Try refreshing the page at 7:00 AM for new availability",
                    "Check individual trailhead pages directly",
                    "Look for cancellation notifications or alternative booking paths"
                };
            }
            else
            {
                analysis.IsFailureValid = true;
                analysis.Reason = "Failure appears CORRECT - No booking options found on page";
                analysis.HealingInstructions = new List<string>
                {
                    "1. Park genuinely appears to be closed for reservations",
                    "2. Check park website for reopening dates",
                    "3. Try again during booking window (usually 7:00 AM)",
                    "4. Consider alternative parks or dates"
                };
                analysis.ConfidenceLevel = "MEDIUM";
            }
        }
        else if (failureUpper.Contains("SOLD OUT") || failureUpper.Contains("NO AVAILABILITY"))
        {
            analysis.IsFailureValid = true;
            analysis.Reason = "Legitimate sold out condition";
            analysis.HealingInstructions = new List<string>
            {
                "1. Check for cancellations by refreshing frequently",
                "2. Try alternative dates nearby",
                "3. Set up monitoring for availability changes"
            };
        }
        else
        {
            analysis.IsFailureValid = false;
            analysis.Reason = "Unknown failure type - requires investigation";
            analysis.HealingInstructions = new List<string>
            {
                "1. Take screenshot for manual review",
                "2. Check page for any interactive elements",
                "3. Try alternative selectors or approaches"
            };
            analysis.ConfidenceLevel = "LOW";
        }
        
        return analysis;
    }
    
    private List<string> ExtractBookingElements(string pageContent)
    {
        var elements = new List<string>();
        var contentUpper = pageContent.ToUpper();
        
        var bookingKeywords = new[] { "BOOK A PASS", "BOOK NOW", "RESERVE", "SELECT", "CHOOSE", "AVAILABLE" };
        
        foreach (var keyword in bookingKeywords)
        {
            if (contentUpper.Contains(keyword))
            {
                elements.Add($"Found: {keyword}");
            }
        }
        
        return elements;
    }
    
    private FailureAnalysis ParseFailureAnalysisFromText(string aiResponse, string failureMessage, string pageContent)
    {
        var analysis = new FailureAnalysis();
        var responseUpper = aiResponse.ToUpper();
        
        // Extract key information from AI response
        analysis.IsFailureValid = !responseUpper.Contains("FALSE") && !responseUpper.Contains("INCORRECT");
        analysis.Reason = aiResponse.Length > 200 ? aiResponse.Substring(0, 200) + "..." : aiResponse;
        
        // Extract healing instructions from response
        var lines = aiResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        analysis.HealingInstructions = lines
            .Where(line => line.Contains("1.") || line.Contains("2.") || line.Contains("3.") || 
                          line.Contains("•") || line.Contains("-"))
            .Take(5)
            .ToList();
        
        if (!analysis.HealingInstructions.Any())
        {
            analysis.HealingInstructions.Add("Review AI response and take appropriate action");
        }
        
        return analysis;
    }
    
    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}

// Data models for AI decisions
public class BookingRules
{
    public string BookingStartTime { get; set; } = "";
    public int AdvanceBookingDays { get; set; } = 2;
    public string TimeLimit { get; set; } = "";
    public string[] PassTypes { get; set; } = Array.Empty<string>();
    public int MaxPassesPerDay { get; set; } = 1;
    public bool Transferable { get; set; } = false;
    public string CancellationPolicy { get; set; } = "";
}

public class DateAvailabilityResult
{
    public bool IsAvailable { get; set; }
    public string Reason { get; set; } = "";
    public string RecommendedAction { get; set; } = "";
    public List<string> AlternativeDates { get; set; } = new();
}

public class FormAnalysis
{
    public List<string> FillOrder { get; set; } = new();
    public Dictionary<string, string> FieldMappings { get; set; } = new();
    public List<string> RequiredFields { get; set; } = new();
    public string SubmitButton { get; set; } = "";
    public List<string> Warnings { get; set; } = new();
}

public class ProcessDecision
{
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public TimeSpan WaitTime { get; set; } = TimeSpan.Zero;
    public bool ShouldRetry { get; set; }
    public List<string> SpecificSteps { get; set; } = new();
}

public class FormElement
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Required { get; set; }
    public string Placeholder { get; set; } = "";
}

public class FailureAnalysis
{
    public bool IsFailureValid { get; set; } = true;
    public string Reason { get; set; } = "";
    public List<string> BookingOptionsFound { get; set; } = new();
    public List<string> HealingInstructions { get; set; } = new();
    public string ConfidenceLevel { get; set; } = "MEDIUM";
    public List<string> AlternativeStrategies { get; set; } = new();
}