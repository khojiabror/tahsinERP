using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace tahsinERP.ViewModels
{
    public class OrderTracingViewModel
    {
        public bool IsContractConfirmed { get; set; } = false;
        public bool IsPaymentMade { get; set; } = false ;
        public bool HasArrivedInWarehouse { get; set; } = false;
        public bool IsOrderCompleted { get; set; } = false; 
        public int GetProgressPercentage()
        {
            int completedSteps = 0;
            if (IsContractConfirmed) completedSteps++;
            if (IsPaymentMade) completedSteps++;
            if (HasArrivedInWarehouse) completedSteps++;
            if (IsOrderCompleted) completedSteps++;

            return (completedSteps * 100) / 4; // 4 bosqichdan foiz hisoblash
        }

    }
}