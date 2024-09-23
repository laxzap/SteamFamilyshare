using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SteamFamilyshare
{
    public class FirewallRuleManager
    {
        private readonly string ruleName;
        private readonly string exePath;

        public FirewallRuleManager(string ruleName, string exePath)
        {
            this.ruleName = ruleName;
            this.exePath = exePath;
        }

        public async Task<string> GetRuleStatusAsync()
        {
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.Start();
                    string output = await Task.Run(() => process.StandardOutput.ReadToEnd());
                    process.WaitForExit();

                    if (output.ToLower().Contains(ruleName.ToLower()))
                    {
                        return output.Contains("Enabled:                              Yes") ? "enabled" : "disabled";
                    }
                    else
                    {
                        return "not found"; // Rule does not exist
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error checking rule status: {ex.Message}");
                return "error"; // Return an error status if an exception occurs
            }
        }

        public async Task<bool> CreateRuleAsync()
        {
            string command = $"advfirewall firewall add rule name=\"{ruleName}\" " +
                             $"dir=out action=block program=\"{exePath}\" enable=no";
            return await ExecuteCommandAsync(command);
        }

        public async Task<bool> EnableRuleAsync()
        {
            return await ExecuteCommandAsync($"advfirewall firewall set rule name=\"{ruleName}\" new enable=yes");
        }

        public async Task<bool> DisableRuleAsync()
        {
            return await ExecuteCommandAsync($"advfirewall firewall set rule name=\"{ruleName}\" new enable=no");
        }

        private async Task<bool> ExecuteCommandAsync(string command)
        {
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = command,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.Start();
                await Task.Run(() => process.WaitForExit());
                return process.ExitCode == 0;
            }
        }
    }
}
