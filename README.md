# SteamFamilyshare

SteamFamilyshare is a WPF application that manages firewall rules for Steam and enhances the usability of sharing games in family accounts.

## Features

- **Firewall Rule Management**: Allows enabling and disabling of firewall rules for Steam.
- **Exe Path Configuration**: Users can select and change the path to `steam.exe`.
- **Status Display**: Shows the status of the firewall rule (active or disabled).
- **Stylish User Interface**: The application features an appealing user interface inspired by Steam's design.

## Requirements

- .NET Framework 4.7.2 or higher
- Visual Studio Community 2022 (for development)

## Usage

- **Select Exe Path**: Click the "Select Exe" button to change the path to `steam.exe`.
- **Enable/Disable Firewall Rule**: Use the corresponding buttons to manage the firewall rule.
- **Check Status**: The status of the rule is displayed in the status field.

## Development Notes

- The application integrates the functionalities of the previous `CurrentRuleWindow` class into the main window to simplify the user interface.
- Existing methods were preferred to avoid redundancy.
- The window has fixed dimensions (300x400) for a user-friendly interface.

## Contributions

Contributions are welcome! Please create a fork of the repository and submit a pull request.

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for more information.
