# Email_tools

A tool for sending automated emails to multiple recipients.

## Description
This project supports sending emails to a list of recipients configured in `Email_tools/recipients.json`. Other configuration information can be stored in `Email_tools/appsettings.json`.

## Requirements
- .NET 9

## Installation
1. Clone the repository:
   ```bash
   git clone <repository-url>
   ```
2. Install any required packages (if needed).

## Usage
- Edit the `Email_tools/recipients.json` file to add recipients.
- Edit the configuration in `Email_tools/appsettings.json` as needed.
- Run the application:
   ```bash
   dotnet run --project Email_tools/Email_tools.csproj
   ```

## Note
- Sensitive configuration files (`recipients.json`, `appsettings.json`) are included in `.gitignore` and will not be tracked by git.

## Contributing
Please create a pull request or issue if you want to contribute or report a bug.
