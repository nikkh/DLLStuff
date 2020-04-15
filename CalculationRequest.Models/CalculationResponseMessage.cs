using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CalculationRequest.Models
{
    
    public class CalculationResponseMessage
    {
        private CalculationResponseMessage() { }

        public CalculationResponseMessage(CalculationRequestMessage requestMessage) 
        {
            RequestMessage = requestMessage;
            CalculationRequestStatus = CalculationRequestStatus.NotStarted;
        }

        public CalculationRequestMessage RequestMessage { get; }
        public string Result { get; set; }
        public CalculationRequestStatus CalculationRequestStatus { get; set; }
    }

    public enum CalculationRequestStatus { NotStarted, InProgress, InvalidRequest, Failed, Succeeded}
}
