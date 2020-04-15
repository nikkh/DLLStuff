using CalculationRequest.Models;
using Microsoft.Azure.ServiceBus;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RequestCreator
{
    class Program
    {
        
        static IQueueClient queueClient;

        public static async Task<int> Main(string[] args)
        {

            var configBuilder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
               .AddJsonFile("settings.json", optional: true, reloadOnChange: true)
               .AddEnvironmentVariables();

            IConfiguration configuration = configBuilder.Build();

            var rootCommand = new RootCommand
            {
                new Option<string>(
                    "--messageType",
                    getDefaultValue: () => "CalculateMessage",
                    description: "The type of message to send.  Valid values are QuitMessage and CalculateMessage.  If not supplied this will defualt to CalculateMessage"),
                new Option<string>(
                    "--dllName",
                    description: "The name of the DLL from which you wish to execute a function (DLL will be downloaded from blob storage)"),
                new Option<string>(
                    "--functionName",
                    description: "the name of the function your wish to execute"),
                new Option<string>(
                    "--functionType",
                    description: "The type of function you wish to execute.  Valid values are IntFunction or StringFunction.  If you specify IntFunction you will need to supply a parameter which will be parsed as an integer"),
                new Option<string>(
                    "--parameters",
                    getDefaultValue: () => null,
                    description: "A comma separated string of Int parameters.  Must be supplied for IntFunction calls, ignored for StringFunctions.  "),
            };

            rootCommand.Description = "This command will generate a single calculation request based on the supplied input parameters, and will generate a calculation request message.  " +
                "This will be placed on the configured service bus queue, which will then be picked up by the DLLStuff service and the requested calculation performed.  " +
                "Finally, the results will be written to the blob storage container specified in configuration.  You can see the results in a blob called <requestid>.json.  " +
                "Request Id is written to the console on completion of the command.";
            try
            {
                rootCommand.Handler = CommandHandler.Create<string, string, string, string, string>(async (messageType, dllName, functionName, functionType, parameters) =>
                {
                    try
                    {
                        
                        if (string.IsNullOrEmpty(messageType))
                        {
                            throw new Exception($"--messageType {messageType} must be provided");
                        }
                        if ((messageType.ToLower() != "quitmessage") && (messageType.ToLower() != "calculatemessage"))
                        {
                            throw new Exception($"--messageType {messageType} is invalid.  Valid values are QuitMessage and CalculateMessage");
                        }
                        Console.WriteLine($"The value for --messageType is: {messageType}");

                        
                        if (string.IsNullOrEmpty(dllName))
                        {
                            throw new Exception($"--dllName {dllName} must be provided");
                        }
                        Console.WriteLine($"The value for --dllName is: {dllName}");

                        
                        if (string.IsNullOrEmpty(functionName))
                        {
                            throw new Exception($"--functionName {functionName} must be provided");
                        }
                        Console.WriteLine($"The value for --functionName is: {functionName}");

                       
                        if (string.IsNullOrEmpty(functionType))
                        {
                            throw new Exception($"--functionType {functionType} must be provided");
                        }
                        if ((functionType.ToLower() != "intfunction") && (functionType.ToLower() != "stringfunction"))
                        {
                            throw new Exception($"--messageType {messageType} is invalid.  Valid values are IntFunction and StringFunction");
                        }
                        Console.WriteLine($"The value for --functionType is: {functionType}");

                        if(functionType.ToLower() == "intfunction")
                        {
                            if (string.IsNullOrEmpty(parameters))
                            {
                                throw new Exception($"--parameters {parameters} must be provided when --functionType is {functionType}");
                            }
                        }
                        Console.WriteLine($"The value for --parameters is: {parameters}");
                                               
                       
                        string serviceBusConnectionString = "";
                        string serviceBusQueueName = "";
                        string storageConnectionString = "";
                        string containerName = "";
                        try
                        {
                            storageConnectionString = configuration["StorageConnectionString"];
                            serviceBusConnectionString = configuration["ServiceBusConnectionString"];
                            serviceBusQueueName = configuration["ServiceBusQueueName"];
                            containerName = configuration["ContainerName"];
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Exception caught while accessing configuration: {e.Message}");
                            return;
                        }

                        queueClient = new QueueClient(serviceBusConnectionString, serviceBusQueueName);
                        var crm = new CalculationRequestMessage();
                        crm.RequestId = Guid.NewGuid().ToString();
                        crm.ContainerName = containerName;

                        if (messageType.ToLower() == "quitmessage")
                        {
                            crm.MessageType = MessageType.QuitMessage;
                        }
                        else
                        {
                            crm.MessageType = MessageType.CalculateMessage;
                            crm.DllName = dllName;
                            crm.FunctionName = functionName;

                            if (functionType.ToLower() == "stringfunction")
                            {
                                crm.FunctionType = FunctionType.StringFunction;
                            }
                            else
                            {
                                crm.FunctionType = FunctionType.IntFunction;
                                crm.Parameters = parameters.Split(',');
                                                            }
                        }
                        
                        var body = JsonConvert.SerializeObject(crm).ToString();
                        var message = new Message(Encoding.UTF8.GetBytes(body));

                        Console.WriteLine($"Sending message: {body}");
                        await queueClient.SendAsync(message);
                        await queueClient.CloseAsync();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Done. Your request id was {crm.RequestId}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e.Message);
                    }
                }
                );
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }
            return rootCommand.InvokeAsync(args).Result;

            
            
        }

      
    }
}

