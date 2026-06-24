using Microsoft.EntityFrameworkCore;
using SmartEGov.Application.DTOs;
using SmartEGov.Application.Services;
using SmartEGov.Infrastructure.Data;

namespace SmartEGov.Infrastructure.Services;

public class LocationService : ILocationService
{
    private readonly ApplicationDbContext _context;

    private static readonly Dictionary<string, (double Lat, double Lng)> CityCoordinates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Beirut"] = (33.8938, 35.5018),
        ["Tripoli"] = (34.4367, 35.8497),
        ["Sidon"] = (33.5633, 35.3698),
        ["Saida"] = (33.5633, 35.3698),
        ["Tyre"] = (33.2705, 35.2038),
        ["Sour"] = (33.2705, 35.2038),
        ["Jounieh"] = (33.9808, 35.6178),
        ["Byblos"] = (34.1236, 35.6511),
        ["Jbeil"] = (34.1236, 35.6511),
        ["Baalbek"] = (34.0047, 36.2110),
        ["Zahle"] = (33.8463, 35.9020),
        ["Zahlé"] = (33.8463, 35.9020),
        ["Nabatieh"] = (33.3779, 35.4839),
        ["Nabatiyeh"] = (33.3779, 35.4839),
        ["Batroun"] = (34.2553, 35.6581),
        ["Aley"] = (33.8100, 35.5972),
        ["Beit Mery"] = (33.8572, 35.5822),
        ["Broummana"] = (33.8797, 35.6381),
        ["Kesrwan"] = (33.9808, 35.6178),
        ["Metn"] = (33.8700, 35.5500),
        ["Chouf"] = (33.6900, 35.5100),
        ["Koura"] = (34.3167, 35.8000),
        ["Zgharta"] = (34.3993, 35.8895),
        ["Akkar"] = (34.5333, 36.0833),
        ["Hermel"] = (34.3937, 36.3853),
        ["Rashaya"] = (33.4986, 35.8481),
        ["Hasbaya"] = (33.3964, 35.6847),
        ["Jezzine"] = (33.5444, 35.5833),
        ["Bint Jbeil"] = (33.1167, 35.4333),
        ["Marjayoun"] = (33.3608, 35.5917),
    };

    public LocationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<NearestLocationDto?> GetNearestLocationAsync(int departmentId, string citizenAddress)
    {
        var locations = await _context.DepartmentLocations
            .Include(l => l.Department)
            .Where(l => l.DepartmentId == departmentId)
            .ToListAsync();

        if (locations.Count == 0)
            return null;

        var citizenCoords = ResolveCityCoordinates(citizenAddress);
        if (citizenCoords == null)
        {
            // If we can't resolve the citizen address, return the first location (main branch)
            var fallback = locations.First();
            return new NearestLocationDto
            {
                BranchName = fallback.BranchName,
                DepartmentName = fallback.Department.Name,
                City = fallback.City,
                Address = fallback.Address,
                PhoneNumber = fallback.PhoneNumber,
                Latitude = fallback.Latitude,
                Longitude = fallback.Longitude,
                DistanceKm = 0
            };
        }

        var nearest = locations
            .Select(l => new
            {
                Location = l,
                Distance = HaversineDistance(citizenCoords.Value.Lat, citizenCoords.Value.Lng, l.Latitude, l.Longitude)
            })
            .OrderBy(x => x.Distance)
            .First();

        return new NearestLocationDto
        {
            BranchName = nearest.Location.BranchName,
            DepartmentName = nearest.Location.Department.Name,
            City = nearest.Location.City,
            Address = nearest.Location.Address,
            PhoneNumber = nearest.Location.PhoneNumber,
            Latitude = nearest.Location.Latitude,
            Longitude = nearest.Location.Longitude,
            DistanceKm = Math.Round(nearest.Distance, 1)
        };
    }

    private static (double Lat, double Lng)? ResolveCityCoordinates(string address)
    {
        foreach (var city in CityCoordinates)
        {
            if (address.Contains(city.Key, StringComparison.OrdinalIgnoreCase))
                return city.Value;
        }
        return null;
    }

    private static double HaversineDistance(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371; // Earth radius in km
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
