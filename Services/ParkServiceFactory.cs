using AutoRes.Models;

namespace AutoRes.Services;

public static class ParkServiceFactory
{
    public static IParkReservationService CreateService(string parkName)
    {
        return parkName switch
        {
            "Joffre Lakes Provincial Park" => new SmartJoffreLakesReservationService(),
            "Garibaldi Provincial Park" => new SmartGaribaldiReservationService(),
            _ => throw new NotSupportedException($"Park '{parkName}' is not yet supported")
        };
    }
    
    public static List<string> GetSupportedParks()
    {
        return new List<string>
        {
            "Joffre Lakes Provincial Park",
            "Garibaldi Provincial Park"
        };
    }
    
    public static string GetParkDescription(string parkName)
    {
        return parkName switch
        {
            "Joffre Lakes Provincial Park" => "Day-use passes required 2 days in advance (7 AM Pacific). Famous for turquoise lakes.",
            "Garibaldi Provincial Park" => "Vehicle passes required for specific trailheads. Three locations: Diamond Head, Rubble Creek, Cheakamus Lake.",
            _ => "Description not available"
        };
    }
}