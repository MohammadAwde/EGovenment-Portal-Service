This small console app lets you test sending SMS via Twilio using the same credentials the application uses.

Prerequisites
- .NET 8 SDK installed
- Twilio account (Account SID and Auth Token)

Set environment variables (recommended):

Windows (PowerShell):
$env:TWILIO_ACCOUNT_SID = "AC..."
$env:TWILIO_AUTH_TOKEN = "..."
$env:TWILIO_FROM_PHONE = "+15017122661"
$env:TWILIO_TEST_TO = "+15558675310"

Linux/macOS:
export TWILIO_ACCOUNT_SID="AC..."
export TWILIO_AUTH_TOKEN="..."
export TWILIO_FROM_PHONE="+15017122661"
export TWILIO_TEST_TO="+15558675310"

Run the sample:

dotnet run --project TwilioSend -- +15558675310 +15017122661

Or rely on env vars:

dotnet run --project TwilioSend

The program will print the Twilio message SID and status on success, or an error and stack trace on failure.

Use this output to debug delivery issues or verify credentials. You can also compare with a raw curl call:

curl 'https://api.twilio.com/2010-04-01/Accounts/<AccountSid>/Messages.json' -X POST -u <AccountSid>:<AuthToken> -d 'To=+RECIPIENT' -d 'From=+SENDER' -d 'Body=Test'
