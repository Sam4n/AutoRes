namespace AutoRes.Models;

public class ParkReservation
{
    public string ParkName { get; set; } = string.Empty;
    public DateTime DesiredDate { get; set; }
    public int NumberOfPeople { get; set; }
    public string? TimeSlot { get; set; }
    public string Email { get; set; } = string.Empty;
}

public class ReservationResult
{
    public bool Success { get; set; }
    public string? ConfirmationNumber { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? ReservationDate { get; set; }
}