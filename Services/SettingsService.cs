using System.Text.Json;
using AutoRes.Models;

namespace AutoRes.Services;

public class SettingsService
{
    private readonly string _settingsPath;
    private UserSettings _settings;
    
    public SettingsService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var autoResPath = Path.Combine(appDataPath, "AutoRes");
        
        try
        {
            Directory.CreateDirectory(autoResPath);
        }
        catch
        {
            // Fallback to local directory if can't create AppData folder
            autoResPath = Directory.GetCurrentDirectory();
        }
        
        _settingsPath = Path.Combine(autoResPath, "settings.json");
        _settings = LoadSettings();
    }
    
    public UserSettings GetSettings() => _settings;
    
    public void SaveEmail(string email)
    {
        _settings.LastEmail = email;
        SaveSettings();
    }
    
    public void SaveLastReservation(string park, DateTime date, int numberOfPeople, string email)
    {
        Console.WriteLine($"[DEBUG] SaveLastReservation called with: park={park}, date={date}, people={numberOfPeople}, email={email}");
        
        _settings.LastSelectedPark = park;
        _settings.LastSelectedDate = date;
        _settings.LastNumberOfPeople = numberOfPeople;
        _settings.LastEmail = email;
        
        Console.WriteLine($"[DEBUG] Settings updated. Calling SaveSettings()...");
        SaveSettings();
    }
    
    public DateTime GetDefaultDate()
    {
        // If we have a saved date that's still in the future, use it
        if (_settings.LastSelectedDate.HasValue && _settings.LastSelectedDate.Value > DateTime.Today)
        {
            return _settings.LastSelectedDate.Value;
        }
        
        // Otherwise, default to 2 days from now (when reservations open)
        return DateTime.Today.AddDays(2);
    }
    
    public int GetDefaultNumberOfPeople()
    {
        return _settings.LastNumberOfPeople ?? 1;
    }
    
    private UserSettings LoadSettings()
    {
        try
        {
            Console.WriteLine($"[DEBUG] Loading settings from: {_settingsPath}");
            
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                Console.WriteLine($"[DEBUG] Found settings file with content: {json}");
                
                var settings = JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
                Console.WriteLine($"[DEBUG] Loaded email: {settings.LastEmail}");
                return settings;
            }
            else
            {
                Console.WriteLine($"[DEBUG] Settings file does not exist, creating new settings");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to load settings: {ex.Message}");
        }
        
        return new UserSettings();
    }
    
    private void SaveSettings()
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions 
            { 
                WriteIndented = true 
            });
            File.WriteAllText(_settingsPath, json);
            
            // Debug: Verify the file was written
            if (File.Exists(_settingsPath))
            {
                var fileContent = File.ReadAllText(_settingsPath);
                Console.WriteLine($"[DEBUG] Settings saved to: {_settingsPath}");
                Console.WriteLine($"[DEBUG] Content: {fileContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Failed to save settings: {ex.Message}");
            Console.WriteLine($"[ERROR] Attempted path: {_settingsPath}");
        }
    }
}