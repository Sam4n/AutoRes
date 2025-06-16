using System.Text.Json;
using AutoRes.Models;
using Spectre.Console;

namespace AutoRes.Services;

public class AutomationLearningService
{
    private readonly string _learningPath;
    private AutomationLearning _learning;
    private AutomationSession? _currentSession;
    
    public AutomationLearningService(string parkName)
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var autoResPath = Path.Combine(appDataPath, "AutoRes", "Learning");
        
        try
        {
            Directory.CreateDirectory(autoResPath);
        }
        catch
        {
            autoResPath = Path.Combine(Directory.GetCurrentDirectory(), "Learning");
            Directory.CreateDirectory(autoResPath);
        }
        
        _learningPath = Path.Combine(autoResPath, $"{parkName.Replace(" ", "_")}_learning.json");
        _learning = LoadLearning(parkName);
    }
    
    public void StartSession(string parkName)
    {
        _currentSession = new AutomationSession
        {
            ParkName = parkName,
            StartTime = DateTime.Now
        };
    }
    
    public void RecordStep(string stepName, string actionType, string? value = null)
    {
        if (_currentSession == null) return;
        
        var step = new AutomationStep
        {
            StepName = stepName,
            Timestamp = DateTime.Now,
            ActionType = actionType,
            Value = value
        };
        
        _currentSession.Steps.Add(step);
        AnsiConsole.MarkupLine($"[dim]Recording step: {stepName}[/]");
    }
    
    public void AddSelector(string stepName, string selectorType, string selectorValue, bool worked)
    {
        if (_currentSession == null) return;
        
        var currentStep = _currentSession.Steps.LastOrDefault(s => s.StepName == stepName);
        if (currentStep == null) return;
        
        var selector = new ElementSelector
        {
            Type = selectorType,
            Value = selectorValue,
            Worked = worked,
            Priority = worked ? 1 : 0
        };
        
        currentStep.Selectors.Add(selector);
        
        // Update learning data
        if (worked)
        {
            if (!_learning.WorkingSelectors.ContainsKey(stepName))
                _learning.WorkingSelectors[stepName] = new List<ElementSelector>();
            
            var existing = _learning.WorkingSelectors[stepName]
                .FirstOrDefault(s => s.Type == selectorType && s.Value == selectorValue);
            
            if (existing != null)
            {
                existing.Priority++;
            }
            else
            {
                _learning.WorkingSelectors[stepName].Add(selector);
            }
        }
        else
        {
            if (!_learning.FailedSelectors.Contains($"{stepName}:{selectorType}:{selectorValue}"))
                _learning.FailedSelectors.Add($"{stepName}:{selectorType}:{selectorValue}");
        }
    }
    
    public List<ElementSelector> GetBestSelectors(string stepName)
    {
        if (_learning.WorkingSelectors.ContainsKey(stepName))
        {
            return _learning.WorkingSelectors[stepName]
                .OrderByDescending(s => s.Priority)
                .ToList();
        }
        
        return new List<ElementSelector>();
    }
    
    public void RecordSuccess(string stepName)
    {
        if (_currentSession == null) return;
        
        var step = _currentSession.Steps.LastOrDefault(s => s.StepName == stepName);
        if (step != null)
        {
            step.Success = true;
        }
    }
    
    public void RecordError(string stepName, string errorMessage)
    {
        if (_currentSession == null) return;
        
        var step = _currentSession.Steps.LastOrDefault(s => s.StepName == stepName);
        if (step != null)
        {
            step.Success = false;
            step.ErrorMessage = errorMessage;
        }
    }
    
    public void EndSession(bool success, string? result = null)
    {
        if (_currentSession == null) return;
        
        _currentSession.Completed = success;
        _currentSession.Result = result;
        
        if (success)
            _learning.SuccessfulRuns++;
        else
            _learning.FailedRuns++;
        
        _learning.LastUpdated = DateTime.Now;
        
        SaveSession();
        SaveLearning();
        
        AnsiConsole.MarkupLine($"[dim]Session recorded. Success rate: {_learning.SuccessfulRuns}/{_learning.SuccessfulRuns + _learning.FailedRuns}[/]");
    }
    
    private void SaveSession()
    {
        if (_currentSession == null) return;
        
        try
        {
            var sessionsPath = Path.Combine(Path.GetDirectoryName(_learningPath)!, "Sessions");
            Directory.CreateDirectory(sessionsPath);
            
            var sessionFile = Path.Combine(sessionsPath, $"{_currentSession.SessionId}.json");
            var json = JsonSerializer.Serialize(_currentSession, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(sessionFile, json);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[yellow]Warning: Could not save session: {ex.Message}[/]");
        }
    }
    
    private AutomationLearning LoadLearning(string parkName)
    {
        try
        {
            if (File.Exists(_learningPath))
            {
                var json = File.ReadAllText(_learningPath);
                return JsonSerializer.Deserialize<AutomationLearning>(json) ?? new AutomationLearning { ParkName = parkName };
            }
        }
        catch
        {
            // If there's any error loading, just return new learning data
        }
        
        return new AutomationLearning { ParkName = parkName };
    }
    
    private void SaveLearning()
    {
        try
        {
            var json = JsonSerializer.Serialize(_learning, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_learningPath, json);
        }
        catch
        {
            // Silently fail if we can't save
        }
    }
    
    public void DisplayLearningStats()
    {
        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Value");
        
        table.AddRow("Successful Runs", _learning.SuccessfulRuns.ToString());
        table.AddRow("Failed Runs", _learning.FailedRuns.ToString());
        table.AddRow("Success Rate", 
            _learning.SuccessfulRuns + _learning.FailedRuns > 0 
                ? $"{(_learning.SuccessfulRuns * 100.0 / (_learning.SuccessfulRuns + _learning.FailedRuns)):F1}%" 
                : "N/A");
        table.AddRow("Known Steps", _learning.WorkingSelectors.Count.ToString());
        table.AddRow("Last Updated", _learning.LastUpdated.ToString("yyyy-MM-dd HH:mm:ss"));
        
        AnsiConsole.Write(table);
    }
}