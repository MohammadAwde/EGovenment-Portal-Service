using System.ComponentModel.DataAnnotations;

namespace SmartEGov.Application.DTOs;

public class CitizenDto
{
    public int Id { get; set; }

    [Display(Name = "National ID")]
    public string NationalId { get; set; } = string.Empty;

    [Required]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Date of Birth")]
    public DateTime DateOfBirth { get; set; }

    [Required]
    [Display(Name = "Address")]
    public string Address { get; set; } = string.Empty;

    [Required]
    [Phone]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    public string? UserId { get; set; }
}
