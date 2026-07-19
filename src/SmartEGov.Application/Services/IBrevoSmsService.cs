namespace SmartEGov.Application.Services;

public interface IBrevoSmsService
{
    Task<bool> SendSmsAsync(string to, string text);
}
