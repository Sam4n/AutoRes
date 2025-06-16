using AutoRes.Models;

namespace AutoRes.Services;

public interface IParkReservationService
{
    string ParkName { get; }
    Task<ReservationResult> MakeReservationAsync(ParkReservation reservation);
    Task<bool> CheckAvailabilityAsync(DateTime date);
}