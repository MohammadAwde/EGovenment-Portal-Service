using System;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Read credentials from environment variables
        var accountSid = Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID");
        var authToken = Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");

        // Allow passing recipient and sender as args for convenience
        var to = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("TWILIO_TEST_TO");
        var from = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("TWILIO_FROM_PHONE");

        if (string.IsNullOrWhiteSpace(accountSid) || string.IsNullOrWhiteSpace(authToken))
        {
            Console.Error.WriteLine("TWILIO_ACCOUNT_SID or TWILIO_AUTH_TOKEN environment variables are not set.");
            return 2;
        }

        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(from))
        {
            Console.Error.WriteLine("Recipient or sender phone number not provided. Provide as args or set TWILIO_TEST_TO and TWILIO_FROM_PHONE env vars.");
            Console.Error.WriteLine("Usage: dotnet run --project TwilioSend -- "+"\"+15558675310\" \"+15017122661\"");
            return 2;
        }

        TwilioClient.Init(accountSid, authToken);

        try
        {
            var message = await MessageResource.CreateAsync(
                body: "Test message from SmartEGov",
                from: new PhoneNumber(from),
                to: new PhoneNumber(to)
            );

            Console.WriteLine($"Message sent. SID={message.Sid}, Status={message.Status}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Failed to send message: " + ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
