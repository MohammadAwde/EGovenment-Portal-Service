namespace SmartEGov.Domain.Entities;


public class DocumentProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "NationalID";

    //Name fields 
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FatherName { get; set; } = string.Empty;
    public string MotherName { get; set; } = string.Empty;
    public string MotherLastName { get; set; } = string.Empty;

    //Identity numbers 
    public string DocumentNumber { get; set; } = string.Empty;
    public string RegistryNumber { get; set; } = string.Empty;


    public DateTime DateOfBirth { get; set; }


    public string Village { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Province { get; set; } = string.Empty;

    //Metadata 
    public string ExtractionMethod { get; set; } = "Manual";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ApplicationUser User { get; set; } = null!;
}
