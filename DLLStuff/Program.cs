using CalculationRequest.Models;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DLLStuff
{
    class Program
    {
        static IQueueClient queueClient;
        static bool shutdownRequested = false;
        static string storageConnectionString;

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
        private delegate IntPtr NicksIntFunction(IntPtr numPointer);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate IntPtr NicksStringFunction();

        #endregion

        static async Task ProcessMessagesAsync(Message message, CancellationToken token)
        {
            var body = Encoding.UTF8.GetString(message.Body);
            ConsoleWriteInColour($"Received message: SequenceNumber:{message.SystemProperties.SequenceNumber} Body:{body}", ConsoleColor.Green);
            var crm = JsonConvert.DeserializeObject<CalculationRequestMessage>(body);
            if (crm.MessageType == MessageType.QuitMessage)
            {
                ConsoleWriteInColour($"Quit Message was received at {DateTime.Now.ToShortTimeString()}", ConsoleColor.Green);
                shutdownRequested = true;
            }
            else
            {
                await ProcessCalculationRequest(crm);
            }
            await queueClient.CompleteAsync(message.SystemProperties.LockToken);
        }

        public static void ConsoleWriteInColour(String message, ConsoleColor colour = ConsoleColor.Gray)
        {
            var oldColor = Console.ForegroundColor;
            Console.ForegroundColor = colour;
            Console.WriteLine(message);
            Console.ForegroundColor = oldColor;
        }


        private static async Task ProcessCalculationRequest(CalculationRequestMessage crm)
        {
            var calculationResponseMessage = new CalculationResponseMessage(crm);
            Console.WriteLine($"ProcessingCalculationRequest at {DateTime.Now.ToShortTimeString()}");
            Console.WriteLine($"Request: {crm}");
            Console.WriteLine($"Current Directory: {Directory.GetCurrentDirectory()}");
            if (ValidateCalculationRequestMessage(crm))
            {
                calculationResponseMessage.CalculationRequestStatus = CalculationRequestStatus.InProgress;
            }
           
            ConsoleWriteInColour($"Downloading DLL...{crm.DllName} from container {crm.ContainerName}", ConsoleColor.Green);
            string dllFileName;
            var storageAccount = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(crm.ContainerName);
            try
            {
                Console.WriteLine($"Current directory={Directory.GetCurrentDirectory()}");
                var directoryToCreate = $"{Directory.GetCurrentDirectory()}\\dll";
                Directory.CreateDirectory(directoryToCreate);
                Console.WriteLine($"directory {directoryToCreate} was created");
                dllFileName = $"{directoryToCreate}\\{crm.DllName}";

                var dllBlob = container.GetBlockBlobReference(crm.DllName);
                await dllBlob.FetchAttributesAsync();
                Console.WriteLine($"Blob {crm.DllName} in container {crm.ContainerName} is {dllBlob.Properties.Length} bytes");
                var memoryStream = new MemoryStream();
                await dllBlob.DownloadToStreamAsync(memoryStream);
                using (memoryStream)
                {
                    var fileStream = File.Create(dllFileName);
                    memoryStream.Position = 0;
                    memoryStream.CopyTo(fileStream);
                    fileStream.Close();
                }
                Console.WriteLine($"{crm.DllName} was downloaded to local file system {directoryToCreate}");
                Console.WriteLine($"Directrory Listing:");
                var files = Directory.GetFiles(directoryToCreate);
                foreach (var fileName in files)
                {
                    var fileInfo = new FileInfo($"{fileName}");
                    Console.WriteLine($"Name={fileInfo.Name}, length={fileInfo.Length}");
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Error downloading DLL {crm.DllName} from container {crm.ContainerName}.  Message:{e.Message}", e);
            }
            IntPtr hModule = IntPtr.Zero;
            try
            {
                ConsoleWriteInColour($"DLL Load path is {dllFileName}", ConsoleColor.Green);
                // call a function in the DLL
                hModule = LoadLibrary(dllFileName);
                if (hModule == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Exception($"Failed to load library {dllFileName} (ErrorCode: {errorCode})");
                }
                ConsoleWriteInColour($"{DateTime.Now.ToString()} library {dllFileName} was loaded sucessfully. hModule={hModule}", ConsoleColor.Yellow);

                IntPtr funcaddr = GetProcAddress(hModule, crm.FunctionName);
                if (funcaddr == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new Exception($"Failed to find function {crm.FunctionName} (ErrorCode: {errorCode})");
                }
                ConsoleWriteInColour($"{DateTime.Now.ToString()} function {crm.FunctionName} found in library {dllFileName} address={funcaddr}", ConsoleColor.Yellow);

                if (crm.FunctionType == FunctionType.StringFunction)
                {
                    NicksStringFunction stringFunction = Marshal.GetDelegateForFunctionPointer<NicksStringFunction>(funcaddr) as NicksStringFunction;
                    IntPtr stringResultPtr = stringFunction();
                    string stringResult = Marshal.PtrToStringBSTR(stringResultPtr);
                    ConsoleWriteInColour($"{DateTime.Now.ToString()} function {crm.FunctionName} returned \"{stringResult}\"", ConsoleColor.Cyan);
                    calculationResponseMessage.CalculationRequestStatus = CalculationRequestStatus.Succeeded;
                    calculationResponseMessage.Result = stringResult;
                }

                if (crm.FunctionType == FunctionType.IntFunction)
                {
                    if (Int32.TryParse(crm.Parameters[0], out Int32 number))
                    {
                
                        IntPtr numPointer = new IntPtr(number);
                        NicksIntFunction intFunction = Marshal.GetDelegateForFunctionPointer<NicksIntFunction>(funcaddr) as NicksIntFunction;
                        IntPtr intResultPtr = intFunction(numPointer);
                        Int32 intResult = intResultPtr.ToInt32();
                        ConsoleWriteInColour($"{DateTime.Now.ToString()} function {crm.FunctionName} returned \"{intResult}\"", ConsoleColor.Cyan);
                        calculationResponseMessage.CalculationRequestStatus = CalculationRequestStatus.Succeeded;
                        calculationResponseMessage.Result = intResult.ToString();
                    }
                    else
                    {
                        ConsoleWriteInColour($"{DateTime.Now.ToString()} function {crm.FunctionName} no parameters supplied for function", ConsoleColor.Red);
                    }
                }

                
                Console.WriteLine($"{DateTime.Now.ToString()} DLLStuff completed normally");
            }
            catch (Exception e) 
            {
               throw new Exception($"Error loading and executing function {crm.FunctionName} from DLL {crm.DllName}.  Message:{e.Message}", e);
            }
            finally
            {
                if (hModule != IntPtr.Zero)
                {
                    FreeLibrary(hModule);
                    ConsoleWriteInColour($"{DateTime.Now.ToString()} library {dllFileName} was unloaded", ConsoleColor.Yellow);
                };
            }
            // Now serialize the result object to blob storage
            try
            {
                var resultsBlob = container.GetBlockBlobReference($"{crm.RequestId}.json");
                var results = JsonConvert.SerializeObject(calculationResponseMessage);
                await resultsBlob.UploadTextAsync(results);
            }
            catch(Exception e)
            {
                throw new Exception($"Error uploading results to blob storage.  Function {crm.FunctionName} in DLL {crm.DllName} was called sucessfully.  Message:{e.Message}", e);
            }

        }

        private static bool ValidateCalculationRequestMessage(CalculationRequestMessage crm)
        {
            if (String.IsNullOrEmpty(crm.RequestId)) throw new Exception("RequestId cannot be null or empty");
            if (String.IsNullOrEmpty(crm.FunctionName)) throw new Exception("Function Name cannot be null or empty");
            if (String.IsNullOrEmpty(crm.DllName)) throw new Exception("Dll Name cannot be null or empty");
            if (String.IsNullOrEmpty(crm.ContainerName)) throw new Exception("Container Name cannot be null or empty");
            return true;
        }

        static Task ExceptionReceivedHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            Console.WriteLine($"Message handler encountered an exception {exceptionReceivedEventArgs.Exception}.");
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;
            Console.WriteLine("Exception context for troubleshooting:");
            Console.WriteLine($"- Endpoint: {context.Endpoint}");
            Console.WriteLine($"- Entity Path: {context.EntityPath}");
            Console.WriteLine($"- Executing Action: {context.Action}");
            return Task.CompletedTask;
        }

        static async Task Main(string[] args)
        {
            ConsoleWriteInColour($"{DateTime.Now.ToString()} Welcome to Nick's DLL Experiments...", ConsoleColor.Green);
            var configBuilder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
               .AddJsonFile("settings.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables();

            IConfiguration configuration = configBuilder.Build();
            string serviceBusConnectionString = "";
            string serviceBusQueueName = "";
            
            try
            {
                storageConnectionString = configuration["StorageConnectionString"];
                serviceBusConnectionString = configuration["ServiceBusConnectionString"];
                serviceBusQueueName = configuration["ServiceBusQueueName"];
            }
            catch (Exception e)
            {
                ConsoleWriteInColour($"Exception caught while accessing configuration: {e.Message}", ConsoleColor.Red);
                return;
            }
            try
            {
                queueClient = new QueueClient(serviceBusConnectionString, serviceBusQueueName);
                var messageHandlerOptions = new MessageHandlerOptions(ExceptionReceivedHandler)
                {
                    MaxConcurrentCalls = 1,
                    AutoComplete = false
                };
                queueClient.RegisterMessageHandler(ProcessMessagesAsync, messageHandlerOptions);
                while (!shutdownRequested)
                {
                    Console.WriteLine($"{DateTime.Now.ToString()} - Waiting for messages on queue {serviceBusQueueName}");
                    Thread.Sleep(15000);
                }
            }
            catch (Exception e)
            {
                ConsoleWriteInColour($"Exception caught while registering message handler: {e.Message}", ConsoleColor.Red);
                return;
            }
            finally
            {
                await queueClient.CloseAsync();
            }

            return;
        }

        private static void WriteEnvironmentData()
        {
            string str;
            string nl = Environment.NewLine;
            //
            Console.WriteLine("*************************************************************************************************************");
            Console.WriteLine("-- Environment members --");

            //  Invoke this sample with an arbitrary set of command line arguments.
            Console.WriteLine("CommandLine: {0}", Environment.CommandLine);

            string[] arguments = Environment.GetCommandLineArgs();
            Console.WriteLine("GetCommandLineArgs: {0}", String.Join(", ", arguments));

            //  <-- Keep this information secure! -->
            Console.WriteLine("CurrentDirectory: {0}", Environment.CurrentDirectory);

            Console.WriteLine("ExitCode: {0}", Environment.ExitCode);

            Console.WriteLine("HasShutdownStarted: {0}", Environment.HasShutdownStarted);

            //  <-- Keep this information secure! -->
            Console.WriteLine("MachineName: {0}", Environment.MachineName);

            Console.WriteLine("NewLine: {0}  first line{0}  second line{0}  third line",
                                  Environment.NewLine);

            Console.WriteLine("OSVersion: {0}", Environment.OSVersion.ToString());

            Console.WriteLine("StackTrace: '{0}'", Environment.StackTrace);

            //  <-- Keep this information secure! -->
            Console.WriteLine("SystemDirectory: {0}", Environment.SystemDirectory);

            Console.WriteLine("TickCount: {0}", Environment.TickCount);

            //  <-- Keep this information secure! -->
            Console.WriteLine("UserDomainName: {0}", Environment.UserDomainName);

            Console.WriteLine("UserInteractive: {0}", Environment.UserInteractive);

            //  <-- Keep this information secure! -->
            Console.WriteLine("UserName: {0}", Environment.UserName);

            Console.WriteLine("Version: {0}", Environment.Version.ToString());

            Console.WriteLine("WorkingSet: {0}", Environment.WorkingSet);

            //  No example for Exit(exitCode) because doing so would terminate this example.

            //  <-- Keep this information secure! -->
            string query = "My system drive is %SystemDrive% and my system root is %SystemRoot%";
            str = Environment.ExpandEnvironmentVariables(query);
            Console.WriteLine("ExpandEnvironmentVariables: {0}  {1}", nl, str);

            Console.WriteLine("GetEnvironmentVariable: {0}  My temporary directory is {1}.", nl,
                                   Environment.GetEnvironmentVariable("TEMP"));

            Console.WriteLine("GetEnvironmentVariables: ");
            IDictionary environmentVariables = Environment.GetEnvironmentVariables();
            foreach (DictionaryEntry de in environmentVariables)
            {
                Console.WriteLine("  {0} = {1}", de.Key, de.Value);
            }

            Console.WriteLine("GetFolderPath: {0}",
                         Environment.GetFolderPath(Environment.SpecialFolder.System));

            string[] drives = Environment.GetLogicalDrives();
            Console.WriteLine("GetLogicalDrives: {0}", String.Join(", ", drives));
            Console.WriteLine("*************************************************************************************************************");
        }
    }
}
