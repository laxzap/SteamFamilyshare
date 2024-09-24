using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamFamilyshare
{
    public partial class MainWindow : Window
    {
        private const string RuleName = "SteamShareLibrary";
        private const int PollingInterval = 1000; // Polling interval in milliseconds
        private CancellationTokenSource cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            // Check if the application is running with administrator rights
            if (!IsRunAsAdministrator())
            {
                RestartAsAdministrator();
                return;
            }

            InitializeApplication();
        }

        private void InitializeApplication()
        {
            string savedExePath = LoadSavedExePath();
            CheckAndSelectExe(ref savedExePath);

            // Check if the firewall rule exists, if not, create it
            if (!IsFirewallRuleExists(RuleName))
            {
                CreateFirewallRule(savedExePath);
            }

            StartRealTimeStatusCheck();
        }

        private void CheckAndSelectExe(ref string savedExePath)
        {
            // While the user does not select a valid steam.exe
            while (string.IsNullOrEmpty(savedExePath) || Path.GetFileName(savedExePath).Equals("steam.exe", StringComparison.OrdinalIgnoreCase) == false)
            {
                savedExePath = SelectExeFile();

                if (string.IsNullOrEmpty(savedExePath))
                {
                    Application.Current.Shutdown();
                    return;
                }
                else if (!Path.GetFileName(savedExePath).Equals("steam.exe", StringComparison.OrdinalIgnoreCase))
                {
                    ShowAlert("Please select only steam.exe.");
                }
            }

            selectedExePathTextBox.Text = savedExePath; // Update UI
            SaveExePath(savedExePath); // Save path
        }

        private async void StartRealTimeStatusCheck()
        {
            cancellationTokenSource = new CancellationTokenSource();
            try
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    await UpdateButtonColorsAsync();
                    await Task.Delay(PollingInterval, cancellationTokenSource.Token);
                }
            }
            catch (TaskCanceledException)
            {
                // Handle cancellation if needed
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            cancellationTokenSource?.Cancel(); // Cancel status check on window close
        }

        private async void OnClick(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            string ruleStatus = await GetFirewallRuleStatusAsync(RuleName);

            if (clickedButton == enableNetwork && ruleStatus != "disabled")
            {
                await ExecuteNetshCommandAsync($"advfirewall firewall set rule name=\"{RuleName}\" new enable=no");
                ShowAlert("Rule disabled (Network access allowed).");
            }
            else if (clickedButton == disableNetwork && ruleStatus != "enabled")
            {
                await ExecuteNetshCommandAsync($"advfirewall firewall set rule name=\"{RuleName}\" new enable=yes");
                ShowAlert("Rule enabled (Network access blocked).");
            }

            await UpdateButtonColorsAsync();
        }

        private static void CreateFirewallRule(string exePath) // Marked as static
        {
            string command = $"advfirewall firewall add rule name=\"{RuleName}\" dir=out action=block program=\"{exePath}\" enable=no";
            ExecuteNetshCommandAsync(command).Wait();
            ShowAlert("Firewall rule created successfully.");
        }

        private static async Task<string> GetFirewallRuleStatusAsync(string ruleName) // Marked as static
        {
            return await Task.Run(() =>
            {
                try
                {
                    using (var process = new Process())
                    {
                        process.StartInfo.FileName = "powershell.exe";
                        process.StartInfo.Arguments = $"-Command \"Get-NetFirewallRule -DisplayName '{ruleName}' | Select-Object -ExpandProperty Enabled\"";
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;

                        process.Start();
                        string output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();

                        return output.Equals("True", StringComparison.OrdinalIgnoreCase) ? "enabled"
                            : output.Equals("False", StringComparison.OrdinalIgnoreCase) ? "disabled"
                            : "not found";
                    }
                }
                catch (Exception ex)
                {
                    ShowAlert("Error checking rule status: " + ex.Message);
                    return "not found";
                }
            });
        }

        private async Task UpdateButtonColorsAsync()
        {
            string ruleStatus = await GetFirewallRuleStatusAsync(RuleName);

            // Perform UI updates on the main thread
            Application.Current.Dispatcher.Invoke(() =>
            {
                enableNetwork.Background = ruleStatus == "disabled" ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Gray);
                disableNetwork.Background = ruleStatus == "enabled" ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Gray);

                // Update the rule status text block
                ruleStatusTextBlock.Text = ruleStatus == "enabled" ? "Network disabled" : "Network enabled";
                ruleStatusTextBlock.Background = ruleStatus == "enabled" ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Green);
            });
        }

        private static bool IsRunAsAdministrator() // Marked as static
        {
            return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RestartAsAdministrator() // Marked as static
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule.FileName,
                    UseShellExecute = true,
                    Verb = "runas"
                });
                Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                ShowAlert("This application requires administrator privileges: " + ex.Message);
                Application.Current.Shutdown();
            }
        }

        private static string SelectExeFile() // Marked as static
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe",
                Title = "Select steam.exe"
            };

            // Show the dialog and check if the user selected a file
            if (openFileDialog.ShowDialog() == true)
            {
                // Check if the selected file is "steam.exe"
                if (Path.GetFileName(openFileDialog.FileName).Equals("steam.exe", StringComparison.OrdinalIgnoreCase))
                {
                    return openFileDialog.FileName; // Return the file path if the file matches
                }
                else
                {
                    ShowAlert("Please select only steam.exe."); // Show warning
                    return null; // Return null to indicate that the selection was invalid
                }
            }

            return null; // Return null if the dialog is canceled
        }

        private static void SaveExePath(string exePath) // Marked as static
        {
            try
            {
                Properties.Settings.Default.ExePath = exePath;
                Properties.Settings.Default.Save(); // Save path in settings
            }
            catch (Exception ex)
            {
                ShowAlert("Error saving file path: " + ex.Message);
            }
        }

        private static string LoadSavedExePath() // Marked as static
        {
            return Properties.Settings.Default.ExePath;
        }

        private static bool IsFirewallRuleExists(string ruleName) // Marked as static
        {
            try
            {
                using (var process = new Process())
                {
                    process.StartInfo.FileName = "netsh";
                    process.StartInfo.Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"";
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Use IndexOf for case-insensitive search
                    return output.IndexOf(ruleName, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error checking firewall rule: " + ex.Message);
                return false;
            }
        }


        private static async Task ExecuteNetshCommandAsync(string command) // Marked as static
        {
            await Task.Run(() =>
            {
                try
                {
                    using (var process = new Process())
                    {
                        process.StartInfo.FileName = "netsh";
                        process.StartInfo.Arguments = command;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;

                        process.Start();
                        process.WaitForExit();
                    }
                }
                catch (Exception ex)
                {
                    ShowAlert("Error executing command: " + ex.Message);
                }
            });
        }

        private static void ShowAlert(string message) // Marked as static
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Opens a dialog to select steam.exe and updates the path in the text box.
        /// </summary>
        private void SelectExeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exePath = SelectExeFile(); // Open file dialog

                if (!string.IsNullOrEmpty(exePath))
                {
                    selectedExePathTextBox.Text = exePath; // Update UI
                    SaveExePath(exePath); // Save path
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Error selecting file: " + ex.Message);
            }
        }
    }
}
