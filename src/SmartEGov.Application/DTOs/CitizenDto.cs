using System.ComponentModel.DataAnnotations;

namespace SmartEGov.Application.DTOs;

public class CitizenDto
{
    public int Id { get; set; }

    public string NationalId { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime DateOfBirth { get; set; }

    [Required]
    public string Address { get; set; } = string.Empty;

    [Required]
    [Phone]
    public string PhoneNumber { get; set; } = string.Empty;

    public string? UserId { get; set; }
}
