using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace SmartEGov.Application.DTOs;

public class DocumentProfileDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "NationalID";

    // Name fields 
    [Required(ErrorMessage = "First name is required")]
    [Display(Name = "First Name (الاسم)")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [Display(Name = "Last Name (اللقب)")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Father's name is required")]
    [Display(Name = "Father's Name (اسم الأب)")]
    public string FatherName { get; set; } = string.Empty;

    [Display(Name = "Mother's First Name (اسم الأم)")]
    public string MotherName { get; set; } = string.Empty;

    [Display(Name = "Mother's Last Name (لقب الأم)")]
    public string MotherLastName { get; set; } = string.Empty;

    // Identity 
    [Display(Name = "ID Number (رقم الهوية)")]
    public string DocumentNumberMasked { get; set; } = string.Empty;

    [Display(Name = "Registry Number (رقم السجل)")]
    public string RegistryNumber { get; set; } = string.Empty;

    // Personal details 
    [Required(ErrorMessage = "Date of birth is required")]
    [DataType(DataType.Date)]
    [Display(Name = "Date of Birth (تاريخ الولادة)")]
    public DateTime DateOfBirth { get; set; }

    // Lebanese administrative location 
    [Display(Name = "Village (البلدة)")]
    public string Village { get; set; } = string.Empty;

    [Display(Name = "District (القضاء)")]
    public string District { get; set; } = string.Empty;

    [Display(Name = "Province (المحافظة)")]
    public string Province { get; set; } = string.Empty;

    // Metadata 
    public string ExtractionMethod { get; set; } = "Manual";
    public DateTime CreatedAt { get; set; }
}

public class AutoFillResultDto
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FatherName { get; set; } = string.Empty;
    public string MotherName { get; set; } = string.Empty;
    public string MotherLastName { get; set; } = string.Empty;

    public string DocumentNumberMasked { get; set; } = string.Empty;
    public string RegistryNumber { get; set; } = string.Empty;

    public DateTime DateOfBirth { get; set; }

    public string Village { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;
    public string ExtractionMethod { get; set; } = string.Empty;
    public bool MayBeOutdated { get; set; }
}

public class OcrUploadRequest
{
    [Required]
    public IFormFile IdImage { get; set; } = null!;

    [Required]
    public string DocumentType { get; set; } = "NationalID";
}
