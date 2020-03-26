using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Runtime.InteropServices;


namespace DLLStuff
{
    class Program
    {
        #region Win32 APIs
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]


        private static extern bool FreeLibrary(IntPtr hModule);
        #endregion

        #region delegates
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate bool IsHibernateAllowed();
        #endregion

        static void Main(string[] args)
        {
            Console.WriteLine($"{DateTime.Now.ToString()} Welcome to Nick's DLL Experiments");

            var configBuilder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
               .AddJsonFile("settings.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables();
            IConfiguration configuration = configBuilder.Build();

            var storageConnectionString = configuration["StorageConnectionString"];
            var inboundContainer = configuration["RunContext:InboundContainer"];
            var outboundContainer = configuration["RunContext:OutboundContainer"];
            var dllName = configuration["RunContext:DLLName"];
            var functionName = configuration["RunContext:FunctionName"];
            var inboundBlobName = configuration["RunContext:InboundBlobName"];
            var outboundBlobPrefix = configuration["RunContext:OutboundBlobPrefix"];
            var outboundBlobSuffix = configuration["RunContext:OutboundBlobSuffix"];
            var dllPresentInInboundContainer = configuration["RunContext:DllPresentInInboundContainer"];
            Console.WriteLine("Configuration:");
            Console.WriteLine($"storage connection string startswith={storageConnectionString.Substring(0, 30)}");
            Console.WriteLine($"inbound container={inboundContainer}");
            Console.WriteLine($"inbound blob name ={inboundBlobName}");
            Console.WriteLine($"outbound container={outboundContainer}");
            Console.WriteLine($"outbound blob prefix={outboundBlobPrefix}");
            Console.WriteLine($"outbound blob suffix={outboundBlobSuffix}");
            Console.WriteLine();
            Console.WriteLine($"dll name={dllName}");
            Console.WriteLine($"function name={functionName}");
            try
            {

                
                IntPtr hModule = LoadLibrary(dllName);
                if (hModule == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Exception($"Failed to find load library {dllName} (ErrorCode: {errorCode})");
                }
                Console.WriteLine($"{DateTime.Now.ToString()} library {dllName} was loaded sucessfully. hModule={hModule}");

                IntPtr funcaddr = GetProcAddress(hModule, functionName);
                if (funcaddr == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Exception($"Failed to find function {functionName} (ErrorCode: {errorCode})");
                }
                Console.WriteLine($"{DateTime.Now.ToString()} function {functionName} found in library {dllName} address={funcaddr}");

                IsHibernateAllowed isHibernateAllowed = Marshal.GetDelegateForFunctionPointer(funcaddr, typeof(IsHibernateAllowed)) as IsHibernateAllowed;
                bool hibernateAllowed = isHibernateAllowed.Invoke();
                Console.WriteLine($"{DateTime.Now.ToString()} function {functionName} executed sucessfully!");
                if (hibernateAllowed) Console.WriteLine($"{DateTime.Now.ToString()} Hibernate Allowed!"); 
                else Console.WriteLine($"{DateTime.Now.ToString()} Hibernate NOT Allowed!");


                if (hModule != IntPtr.Zero)
                {
                    FreeLibrary(hModule);
                    Console.WriteLine($"{DateTime.Now.ToString()} library {dllName} was unloaded");
                };
                Console.WriteLine($"{DateTime.Now.ToString()} DLLStuff completed normally");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error {e.Message}");
            }
           
           
        
        }
      }
}
