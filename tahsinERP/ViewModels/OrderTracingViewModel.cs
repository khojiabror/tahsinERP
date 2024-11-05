using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace tahsinERP.ViewModels
{
    public class OrderTracingViewModel
    {
        public DateTime CreatedOrderDT { get; set; } = DateTime.MinValue;
        public DateTime IssuedContractDT { get; set; } = DateTime.MinValue;
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
        public List<OrderTracingPart> Parts { get; set; }
        public OrderTracingViewModel()
        {
            Parts = new List<OrderTracingPart>();
        }

    }
    public class OrderTracingPart
    {
        public string ItemName { get; set; }
        public double Amount { get; set; }
        public bool OrderStatus { get; set; } = false;
        public bool ContractStatus { get; set; } = false;
        public bool PaymentStatus { get; set; } = false;
        public bool IncomeStatus { get; set; } = false;
    }
}