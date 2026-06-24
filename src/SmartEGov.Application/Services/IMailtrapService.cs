namespace SmartEGov.Application.Services;

public interface IMailtrapService
{
    /// <summary>
    /// Send an HTML email via Mailtrap Send API. Returns true when Mailtrap responded successfully.
    /// </summary>
    Task<bool> SendAsync(string to, string subject, string html);
}
