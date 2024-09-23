using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SteamFamilyshare
{
    public partial class MainWindow : Window
    {
        private string savedExePath = string.Empty;
        private const string RuleName = "SteamShareLibrary";
        private const int PollingInterval = 1000; // Faster polling interval

        public MainWindow()
        {
            InitializeComponent();

            if (!IsRunAsAdministrator())
            {
                RestartAsAdministrator();
                return;
            }

            InitializeApplication();
        }

        private void InitializeApplication()
        {
            savedExePath = LoadSavedExePath();
            CheckAndSelectExe();

            if (!IsFirewallRuleExists(RuleName))
            {
                CreateFirewallRule();
            }

            UpdateButtonColors();
            StartRealTimeStatusCheck();
        }

        private void CheckAndSelectExe()
        {
            while (string.IsNullOrEmpty(savedExePath) || Path.GetFileName(savedExePath).ToLower() != "steam.exe")
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
            SaveExePath(savedExePath);
        }


        private async void StartRealTimeStatusCheck()
        {
            while (true)
            {
                UpdateButtonColors();
                await Task.Delay(PollingInterval);
            }
        }

        private async void OnClick(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            string ruleStatus = await GetFirewallRuleStatusAsync(RuleName);

            if (clickedButton == enableNetwork && ruleStatus != "disabled")
            {
                await ExecuteNetshCommandAsync($"advfirewall firewall set rule name=\"{RuleName}\" new enable=no");
                ShowAlert("The rule has been disabled (network access allowed).");
            }
            else if (clickedButton == disableNetwork && ruleStatus != "enabled")
            {
                await ExecuteNetshCommandAsync($"advfirewall firewall set rule name=\"{RuleName}\" new enable=yes");
                ShowAlert("The rule has been enabled (network access blocked).");
            }

            UpdateButtonColors();
        }

        private void CreateFirewallRule()
        {
            string command = $"advfirewall firewall add rule name=\"{RuleName}\" dir=out action=block program=\"{savedExePath}\" enable=no";
            ExecuteNetshCommandAsync(command).Wait();
            ShowAlert("Firewall rule successfully created.");
        }

        private async Task<string> GetFirewallRuleStatusAsync(string ruleName)
        {
            return await Task.Run(() =>
            {
                try
                {
                    Process process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "powershell.exe",
                            Arguments = $"-Command \"Get-NetFirewallRule -DisplayName '{ruleName}' | Select-Object -ExpandProperty Enabled\"",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();

                    return output.Equals("True", StringComparison.OrdinalIgnoreCase) ? "enabled"
                        : output.Equals("False", StringComparison.OrdinalIgnoreCase) ? "disabled"
                        : "not found";
                }
                catch (Exception ex)
                {
                    ShowAlert("Error checking rule status: " + ex.Message);
                    return "not found";
                }
            });
        }

        private async void UpdateButtonColors()
        {
            string ruleStatus = await GetFirewallRuleStatusAsync(RuleName);

            Application.Current.Dispatcher.Invoke(() =>
            {
                enableNetwork.Background = ruleStatus == "disabled" ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Gray);
                disableNetwork.Background = ruleStatus == "enabled" ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Gray);

                ruleStatusTextBlock.Text = ruleStatus == "enabled" ? "Network disabled" : "Network enabled";
                ruleStatusTextBlock.Background = ruleStatus == "enabled" ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Green);
            });
        }

        private bool IsRunAsAdministrator() =>
            new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        private void RestartAsAdministrator()
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
                ShowAlert("This app requires administrator rights: " + ex.Message);
                Application.Current.Shutdown();
            }
        }

        private string SelectExeFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe",
                Title = "Select steam.exe"
            };

            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                return openFileDialog.FileName;
            }
            return null;
        }

        private void SaveExePath(string exePath)
        {
            try
            {
                File.WriteAllText("SavedExePath.txt", exePath);
            }
            catch (Exception ex)
            {
                ShowAlert("Error saving the file path: " + ex.Message);
            }
        }

        private string LoadSavedExePath() => File.Exists("SavedExePath.txt") ? File.ReadAllText("SavedExePath.txt") : null;

        private bool IsFirewallRuleExists(string ruleName)
        {
            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                return output.Contains(ruleName);
            }
            catch (Exception ex)
            {
                ShowAlert("Error checking firewall rule: " + ex.Message);
                return false;
            }
        }

        private async Task ExecuteNetshCommandAsync(string command)
        {
            await Task.Run(() =>
            {
                try
                {
                    Process process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = "netsh",
                            Arguments = command,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        ShowAlert($"Command failed: {error}");
                    }
                    else
                    {
                        Console.WriteLine(output);
                    }
                }
                catch (Exception ex)
                {
                    ShowAlert("Error executing firewall command: " + ex.Message);
                }
            });
        }

        private void ShowAlert(string message)
        {
            MessageBox.Show(message, "Alert", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SelectExeButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedPath = SelectExeFile();
            if (!string.IsNullOrEmpty(selectedPath) && Path.GetFileName(selectedPath).Equals("steam.exe", StringComparison.OrdinalIgnoreCase))
            {
                selectedExePathTextBox.Text = selectedPath;
                SaveExePath(selectedPath);
            }
            else
            {
                ShowAlert("Please select only steam.exe.");
            }
        }
    }
}
