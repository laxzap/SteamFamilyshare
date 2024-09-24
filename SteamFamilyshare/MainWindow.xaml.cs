using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
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
        private const int PollingInterval = 1000; // Polling Intervall
        private CancellationTokenSource cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            // Überprüfen, ob die Anwendung mit Administratorrechten ausgeführt wird
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

            // Überprüfen, ob die Firewallregel existiert und ggf. erstellen
            if (!IsFirewallRuleExists(RuleName))
            {
                CreateFirewallRule(savedExePath);
            }

            StartRealTimeStatusCheck();
        }

        private void CheckAndSelectExe(ref string savedExePath)
        {
            // Solange der Benutzer keine gültige steam.exe auswählt
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
                    ShowAlert("Bitte nur steam.exe auswählen.");
                }
            }

            selectedExePathTextBox.Text = savedExePath; // UI aktualisieren
            SaveExePath(savedExePath); // Pfad speichern
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
            cancellationTokenSource?.Cancel(); // Statusabfrage beim Schließen abbrechen
        }

        private async void OnClick(object sender, RoutedEventArgs e)
        {
            Button clickedButton = sender as Button;
            string ruleStatus = await GetFirewallRuleStatusAsync(RuleName);

            if (clickedButton == enableNetwork && ruleStatus != "disabled")
            {
                await ExecuteNetshCommandAsync($"advfirewall firewall set rule name=\"{RuleName}\" new enable=no");
                ShowAlert("Die Regel wurde deaktiviert (Netzwerkzugriff erlaubt).");
            }
            else if (clickedButton == disableNetwork && ruleStatus != "enabled")
            {
                await ExecuteNetshCommandAsync($"advfirewall firewall set rule name=\"{RuleName}\" new enable=yes");
                ShowAlert("Die Regel wurde aktiviert (Netzwerkzugriff blockiert).");
            }

            await UpdateButtonColorsAsync();
        }

        private void CreateFirewallRule(string exePath)
        {
            string command = $"advfirewall firewall add rule name=\"{RuleName}\" dir=out action=block program=\"{exePath}\" enable=no";
            ExecuteNetshCommandAsync(command).Wait();
            ShowAlert("Firewallregel erfolgreich erstellt.");
        }

        private async Task<string> GetFirewallRuleStatusAsync(string ruleName)
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
                    ShowAlert("Fehler beim Überprüfen des Regelstatus: " + ex.Message);
                    return "not found";
                }
            });
        }

        private async Task UpdateButtonColorsAsync()
        {
            string ruleStatus = await GetFirewallRuleStatusAsync(RuleName);

            // UI-Updates im Hauptthread durchführen
            Application.Current.Dispatcher.Invoke(() =>
            {
                enableNetwork.Background = ruleStatus == "disabled" ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Gray);
                disableNetwork.Background = ruleStatus == "enabled" ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.Gray);

                // Aktualisierung des Regelstatus-Textblocks
                ruleStatusTextBlock.Text = ruleStatus == "enabled" ? "Netzwerk deaktiviert" : "Netzwerk aktiviert";
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
                ShowAlert("Diese Anwendung benötigt Administratorrechte: " + ex.Message);
                Application.Current.Shutdown();
            }
        }

        private string SelectExeFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe",
                Title = "Wählen Sie steam.exe aus"
            };

            return openFileDialog.ShowDialog() == true ? openFileDialog.FileName : null;
        }

        private void SaveExePath(string exePath)
        {
            try
            {
                Properties.Settings.Default.ExePath = exePath;
                Properties.Settings.Default.Save(); // Pfad in den Einstellungen speichern
            }
            catch (Exception ex)
            {
                ShowAlert("Fehler beim Speichern des Dateipfads: " + ex.Message);
            }
        }

        private string LoadSavedExePath() => Properties.Settings.Default.ExePath;

        private bool IsFirewallRuleExists(string ruleName)
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

                    return output.Contains(ruleName);
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Fehler beim Überprüfen der Firewallregel: " + ex.Message);
                return false;
            }
        }

        private async Task ExecuteNetshCommandAsync(string command)
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
                    ShowAlert("Fehler beim Ausführen des Befehls: " + ex.Message);
                }
            });
        }

        private void ShowAlert(string message)
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Öffnet einen Dialog zur Auswahl der steam.exe und aktualisiert den Pfad im Textfeld.
        /// </summary>
        private void SelectExeButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string exePath = SelectExeFile();
                if (!string.IsNullOrEmpty(exePath))
                {
                    selectedExePathTextBox.Text = exePath; // Zeige den Pfad im Textfeld an
                    SaveExePath(exePath); // Speichere den Pfad
                }
            }
            catch (Exception ex)
            {
                ShowAlert("Fehler beim Auswählen der Datei: " + ex.Message);
            }
        }
    }
}
