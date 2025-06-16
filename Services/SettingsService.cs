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
        Directory.CreateDirectory(autoResPath);
        
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
        _settings.LastSelectedPark = park;
        _settings.LastSelectedDate = date;
        _settings.LastNumberOfPeople = numberOfPeople;
        _settings.LastEmail = email;
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
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonSerializer.Deserialize<UserSettings>(json) ?? new UserSettings();
            }
        }
        catch
        {
            // If there's any error loading settings, just return new settings
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
        }
        catch
        {
            // Silently fail if we can't save settings
        }
    }
}