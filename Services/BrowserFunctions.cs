using Microsoft.Playwright;
using System.ComponentModel;
using System.Text.Json;

namespace AutoRes.Services;

/// <summary>
/// Browser automation functions that can be called by AI
/// </summary>
public class BrowserFunctions
{
    private IPage _page;
    
    public BrowserFunctions(IPage page)
    {
        _page = page;
    }
    
    [Description("Click on an element based on its description or text content")]
    public async Task<string> ClickElement(
        [Description("Description of what to click (e.g., 'Book a Pass button', 'Garibaldi Park option')")]
        string description,
        [Description("Optional specific selector if known")]
        string? selector = null)
    {
        try
        {
            ILocator? element = null;
            
            if (!string.IsNullOrEmpty(selector))
            {
                element = _page.Locator(selector);
            }
            else
            {
                // Try multiple strategies based on description
                var searchTerms = ExtractKeywords(description);
                
                foreach (var term in searchTerms)
                {
                    // Try text content
                    element = _page.GetByText(new System.Text.RegularExpressions.Regex(term, System.Text.RegularExpressions.RegexOptions.IgnoreCase));
                    if (await element.IsVisibleAsync())
                        break;
                    
                    // Try button with text
                    element = _page.Locator($"button:has-text('{term}')");
                    if (await element.IsVisibleAsync())
                        break;
                    
                    // Try link with text
                    element = _page.Locator($"a:has-text('{term}')");
                    if (await element.IsVisibleAsync())
                        break;
                    
                    // Try by aria-label
                    element = _page.Locator($"[aria-label*='{term}']");
                    if (await element.IsVisibleAsync())
                        break;
                    
                    element = null;
                }
            }
            
            if (element != null && await element.IsVisibleAsync())
            {
                await element.ClickAsync();
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle, new() { Timeout = 5000 });
                return $"Successfully clicked: {description}";
            }
            
            return $"Could not find element: {description}";
        }
        catch (Exception ex)
        {
            return $"Error clicking {description}: {ex.Message}";
        }
    }
    
    [Description("Fill an input field with the specified value")]
    public async Task<string> FillInput(
        [Description("Description of the field to fill (e.g., 'email field', 'date input')")]
        string fieldDescription,
        [Description("Value to enter in the field")]
        string value,
        [Description("Optional specific selector if known")]
        string? selector = null)
    {
        try
        {
            ILocator? element = null;
            
            if (!string.IsNullOrEmpty(selector))
            {
                element = _page.Locator(selector);
            }
            else
            {
                var keywords = ExtractKeywords(fieldDescription);
                
                foreach (var keyword in keywords)
                {
                    // Try by input type and name/placeholder
                    element = _page.Locator($"input[name*='{keyword}'], input[placeholder*='{keyword}']");
                    if (await element.IsVisibleAsync())
                        break;
                    
                    // Try by label
                    element = _page.Locator($"label:has-text('{keyword}') + input, label:has-text('{keyword}') input");
                    if (await element.IsVisibleAsync())
                        break;
                    
                    element = null;
                }
            }
            
            if (element != null && await element.IsVisibleAsync())
            {
                await element.FillAsync(value);
                return $"Successfully filled {fieldDescription} with: {value}";
            }
            
            return $"Could not find field: {fieldDescription}";
        }
        catch (Exception ex)
        {
            return $"Error filling {fieldDescription}: {ex.Message}";
        }
    }
    
    [Description("Select an option from a dropdown")]
    public async Task<string> SelectOption(
        [Description("Description of the dropdown (e.g., 'number of people', 'park selection')")]
        string dropdownDescription,
        [Description("Option to select")]
        string optionValue,
        [Description("Optional specific selector if known")]
        string? selector = null)
    {
        try
        {
            ILocator? element = null;
            
            if (!string.IsNullOrEmpty(selector))
            {
                element = _page.Locator(selector);
            }
            else
            {
                var keywords = ExtractKeywords(dropdownDescription);
                
                foreach (var keyword in keywords)
                {
                    element = _page.Locator($"select[name*='{keyword}']");
                    if (await element.IsVisibleAsync())
                        break;
                    
                    element = _page.Locator($"label:has-text('{keyword}') + select");
                    if (await element.IsVisibleAsync())
                        break;
                    
                    element = null;
                }
            }
            
            if (element != null && await element.IsVisibleAsync())
            {
                await element.SelectOptionAsync(optionValue);
                return $"Successfully selected '{optionValue}' in {dropdownDescription}";
            }
            
            return $"Could not find dropdown: {dropdownDescription}";
        }
        catch (Exception ex)
        {
            return $"Error selecting option in {dropdownDescription}: {ex.Message}";
        }
    }
    
    [Description("Get all visible interactive elements on the current page")]
    public async Task<string> AnalyzePage()
    {
        try
        {
            var analysis = new
            {
                url = _page.Url,
                title = await _page.TitleAsync(),
                buttons = await GetVisibleElements("button"),
                links = await GetVisibleElements("a"),
                inputs = await GetVisibleElements("input"),
                selects = await GetVisibleElements("select"),
                headings = await GetVisibleElements("h1, h2, h3"),
                importantText = await GetImportantText()
            };
            
            return JsonSerializer.Serialize(analysis, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            return $"Error analyzing page: {ex.Message}";
        }
    }
    
    [Description("Take a screenshot and return the base64 encoded image")]
    public async Task<string> TakeScreenshot()
    {
        try
        {
            var screenshot = await _page.ScreenshotAsync();
            return Convert.ToBase64String(screenshot);
        }
        catch (Exception ex)
        {
            return $"Error taking screenshot: {ex.Message}";
        }
    }
    
    [Description("Navigate to a specific URL")]
    public async Task<string> NavigateToUrl(
        [Description("URL to navigate to")]
        string url)
    {
        try
        {
            await _page.GotoAsync(url);
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            return $"Successfully navigated to: {url}";
        }
        catch (Exception ex)
        {
            return $"Error navigating to {url}: {ex.Message}";
        }
    }
    
    [Description("Wait for an element to appear on the page")]
    public async Task<string> WaitForElement(
        [Description("Description of element to wait for")]
        string elementDescription,
        [Description("Optional specific selector")]
        string? selector = null,
        [Description("Timeout in milliseconds (default 10000)")]
        int timeoutMs = 10000)
    {
        try
        {
            if (!string.IsNullOrEmpty(selector))
            {
                await _page.WaitForSelectorAsync(selector, new() { Timeout = timeoutMs });
                return $"Element appeared: {elementDescription}";
            }
            
            var keywords = ExtractKeywords(elementDescription);
            foreach (var keyword in keywords)
            {
                try
                {
                    await _page.WaitForSelectorAsync($"text='{keyword}'", new() { Timeout = timeoutMs / keywords.Length });
                    return $"Element appeared: {elementDescription}";
                }
                catch
                {
                    continue;
                }
            }
            
            return $"Element did not appear: {elementDescription}";
        }
        catch (Exception ex)
        {
            return $"Error waiting for {elementDescription}: {ex.Message}";
        }
    }
    
    [Description("Check if an element exists and is visible")]
    public async Task<string> CheckElementExists(
        [Description("Description of element to check")]
        string elementDescription,
        [Description("Optional specific selector")]
        string? selector = null)
    {
        try
        {
            bool exists = false;
            
            if (!string.IsNullOrEmpty(selector))
            {
                var element = _page.Locator(selector);
                exists = await element.IsVisibleAsync();
            }
            else
            {
                var keywords = ExtractKeywords(elementDescription);
                foreach (var keyword in keywords)
                {
                    var element = _page.GetByText(keyword);
                    if (await element.IsVisibleAsync())
                    {
                        exists = true;
                        break;
                    }
                }
            }
            
            return exists ? $"Element exists: {elementDescription}" : $"Element not found: {elementDescription}";
        }
        catch (Exception ex)
        {
            return $"Error checking {elementDescription}: {ex.Message}";
        }
    }
    
    private async Task<List<string>> GetVisibleElements(string selector)
    {
        try
        {
            var elements = await _page.Locator(selector).AllAsync();
            var results = new List<string>();
            
            foreach (var element in elements.Take(10)) // Limit to avoid too much data
            {
                if (await element.IsVisibleAsync())
                {
                    var text = await element.TextContentAsync() ?? "";
                    var tagName = await element.EvaluateAsync<string>("el => el.tagName.toLowerCase()");
                    var attrs = await element.EvaluateAsync<string>("el => Array.from(el.attributes).map(a => `${a.name}=\"${a.value}\"`).join(' ')");
                    
                    results.Add($"{tagName}: \"{text.Trim()}\" [{attrs}]");
                }
            }
            
            return results;
        }
        catch
        {
            return new List<string>();
        }
    }
    
    private async Task<List<string>> GetImportantText()
    {
        try
        {
            var selectors = new[] { ".error", ".warning", ".success", ".closed", ".available", ".status" };
            var results = new List<string>();
            
            foreach (var selector in selectors)
            {
                var elements = await _page.Locator(selector).AllAsync();
                foreach (var element in elements.Take(5))
                {
                    if (await element.IsVisibleAsync())
                    {
                        var text = await element.TextContentAsync() ?? "";
                        if (!string.IsNullOrWhiteSpace(text))
                            results.Add($"{selector}: {text.Trim()}");
                    }
                }
            }
            
            return results;
        }
        catch
        {
            return new List<string>();
        }
    }
    
    private string[] ExtractKeywords(string description)
    {
        return description.ToLower()
            .Split(new[] { ' ', ',', '.', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2)
            .ToArray();
    }
}