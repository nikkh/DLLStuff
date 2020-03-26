using System;
using System.Runtime.InteropServices;

namespace DLLStuff
{
    class Program
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string libname);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern bool FreeLibrary(IntPtr hModule);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        delegate bool IsHibernateAllowed();

        

      

        static void Main(string[] args)
        {
            
            Console.WriteLine($"{DateTime.Now.ToString()} Welcome to Nick's DLL Experiments");
            string power_dllName = @"c:\Windows\System32\powrprof.dll";
            string hibernateFunctionName = "IsPwrHibernateAllowed";
            try
            {
                
                IntPtr hModule = LoadLibrary(power_dllName);
                if (hModule == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Exception($"Failed to find load library {power_dllName} (ErrorCode: {errorCode})");
                }
                Console.WriteLine($"{DateTime.Now.ToString()} library {power_dllName} was loaded sucessfully. hModule={hModule}");

                IntPtr funcaddr = GetProcAddress(hModule, hibernateFunctionName);
                if (funcaddr == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Exception($"Failed to find function {hibernateFunctionName} (ErrorCode: {errorCode})");
                }
                Console.WriteLine($"{DateTime.Now.ToString()} function {hibernateFunctionName} found in library {power_dllName} address={funcaddr}");

                IsHibernateAllowed isHibernateAllowed = Marshal.GetDelegateForFunctionPointer(funcaddr, typeof(IsHibernateAllowed)) as IsHibernateAllowed;
                bool hibernateAllowed = isHibernateAllowed.Invoke();
                Console.WriteLine($"{DateTime.Now.ToString()} function {hibernateFunctionName} executed sucessfully!");
                if (hibernateAllowed) Console.WriteLine($"{DateTime.Now.ToString()} Hibernate Allowed!"); 
                else Console.WriteLine($"{DateTime.Now.ToString()} Hibernate NOT Allowed!");


                if (hModule != IntPtr.Zero)
                {
                    FreeLibrary(hModule);
                    Console.WriteLine($"{DateTime.Now.ToString()} library {power_dllName} was unloaded");
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
