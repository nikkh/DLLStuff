using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculationRequest.Models
{
    public class CalculationRequestMessage
    {
        public CalculationRequestMessage() 
        {
            
        }
        public string RequestId { get; set; }
        public string DllName { get; set; }
        public string FunctionName { get; set; }
        public FunctionType FunctionType { get; set; }
        public string[] Parameters { get; set; }
        public string ContainerName { get; set; }
        public MessageType MessageType { get; set; }

        public override string ToString()
        {
            var retval = "";
            retval += $"RequestId={RequestId}, ";
            retval += $"DllName={DllName}, ";
            retval += $"FunctionType={FunctionType}, ";
            retval += $"DllName={DllName}, ";
            retval += $"Parameters=(";
            int i = 1;
            if (Parameters != null)
            {
                foreach (var item in Parameters)
                {
                    retval += item;
                    if (i < Parameters.Length)
                    {
                        retval += ", ";

                    }
                    else
                    {
                        retval += "), ";
                    }
                    i++;
                }
            }
            retval += $"ContainerName={ContainerName}, ";
            retval += $"MessageType={MessageType}, ";
            return retval;
        }
    }


    public enum FunctionType { StringFunction, IntFunction}
    public enum MessageType { QuitMessage, CalculateMessage }
}
