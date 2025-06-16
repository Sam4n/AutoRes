using System.Text.Json.Serialization;

namespace AutoRes.Models;

public class AutomationStep
{
    public string StepName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<ElementSelector> Selectors { get; set; } = new();
    public string? ActionType { get; set; } // click, fill, select, etc.
    public string? Value { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Screenshot { get; set; }
}

public class ElementSelector
{
    public string Type { get; set; } = string.Empty; // css, xpath, text, role
    public string Value { get; set; } = string.Empty;
    public bool Worked { get; set; }
    public int Priority { get; set; }
}

public class AutomationSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public DateTime StartTime { get; set; } = DateTime.Now;
    public string ParkName { get; set; } = string.Empty;
    public List<AutomationStep> Steps { get; set; } = new();
    public bool Completed { get; set; }
    public string? Result { get; set; }
}

public class AutomationLearning
{
    public string ParkName { get; set; } = string.Empty;
    public Dictionary<string, List<ElementSelector>> WorkingSelectors { get; set; } = new();
    public List<string> FailedSelectors { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public int SuccessfulRuns { get; set; }
    public int FailedRuns { get; set; }
}