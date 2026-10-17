using System;
using System.Reflection;
using tahsinERP.Models;
using tahsinERP.ViewModels.HR;

namespace tahsinERP.Services.HR
{
    /// <summary>
    /// HR_TIMESHEET_LINES / HrTimesheetLineViewModel'dagi Day01..Day31 Hours/Quantity ustunlariga kun raqami bo'yicha murojaat.
    /// Avval har katak uchun <c>GetProperty($"Day{d:D2}Hours")</c> reflection qidiruvi bajarilardi (HRM-P16);
    /// endi PropertyInfo'lar bir marta keshlanadi. Shuningdek qator jamilari (HRM-D03) va entity → VM mapping (HRM-D04) shu yerda.
    /// </summary>
    public static class TimesheetLineAccessor
    {
        public const int MaxDays = 31;


        private static readonly PropertyInfo[] EntityHours = Load(typeof(HR_TIMESHEET_LINES), "Hours");
        private static readonly PropertyInfo[] EntityQty = Load(typeof(HR_TIMESHEET_LINES), "Quantity");
        private static readonly PropertyInfo[] VmHours = Load(typeof(HrTimesheetLineViewModel), "Hours");
        private static readonly PropertyInfo[] VmQty = Load(typeof(HrTimesheetLineViewModel), "Quantity");

        private static PropertyInfo[] Load(Type type, string suffix)
        {
            var props = new PropertyInfo[MaxDays + 1];
            for (int d = 1; d <= MaxDays; d++)
            {
                props[d] = type.GetProperty($"Day{d:D2}{suffix}");
                if (props[d] == null)
                    throw new InvalidOperationException($"{type.Name}.Day{d:D2}{suffix} topilmadi");
            }
            return props;
        }

        public static bool IsValidDay(int day) => day >= 1 && day <= MaxDays;

        public static decimal? GetHours(HR_TIMESHEET_LINES line, int day) => (decimal?)EntityHours[day].GetValue(line);

        public static void SetHours(HR_TIMESHEET_LINES line, int day, decimal? value) => EntityHours[day].SetValue(line, value);

        public static decimal? GetQuantity(HR_TIMESHEET_LINES line, int day) => (decimal?)EntityQty[day].GetValue(line);

        public static void SetQuantity(HR_TIMESHEET_LINES line, int day, decimal? value) => EntityQty[day].SetValue(line, value);

        public static decimal? GetHours(HrTimesheetLineViewModel line, int day) => (decimal?)VmHours[day].GetValue(line);

        public static decimal? GetQuantity(HrTimesheetLineViewModel line, int day) => (decimal?)VmQty[day].GetValue(line);

        /// <summary>
        /// Qator jamilarini qayta hisoblaydi (HRM-B15 — ish kalendari HR sozlamalaridan):
        ///  - ishlangan soat/kun — soat > 0 bo'lgan kunlar;
        ///  - kelmagan kun — faqat ISH kunida 0 soat (dam olish va bayram kunlari sanalmaydi);
        ///  - qo'shimcha soat — ish kunida kunlik me'yordan ortig'i, dam olish/bayram kunida barcha soat;
        ///  - jami miqdor.
        /// </summary>
        public static void RecalculateTotals(HR_TIMESHEET_LINES line, int year, int month)
        {
            var settings = HrSettingsService.Current;
            var norm = settings.DailyNormHours;
            int daysInMonth = DateTime.DaysInMonth(year, month);
            decimal totalH = 0, workD = 0, absent = 0, overtime = 0, totalQ = 0;
            for (int d = 1; d <= daysInMonth; d++)
            {
                bool workingDay = settings.IsWorkingDay(new DateTime(year, month, d));
                var h = GetHours(line, d) ?? 0m;
                var q = GetQuantity(line, d) ?? 0m;
                if (h > 0)
                {
                    totalH += h;
                    workD += 1;
                    if (!workingDay) overtime += h;
                    else if (h > norm) overtime += h - norm;
                }
                else if (workingDay)
                {
                    absent += 1;
                }
                if (q > 0) totalQ += q;
            }
            line.TotalWorkedHours = totalH;
            line.TotalWorkedDays = workD;
            line.AbsentDays = absent;
            line.OvertimeHours = overtime;
            line.TotalQuantity = totalQ;
        }

        /// <summary>Entity → VM (xodim, lavozim va mahsulot navigatsiyalari oldindan yuklangan bo'lishi kerak).</summary>
        public static HrTimesheetLineViewModel ToLineVm(HR_TIMESHEET_LINES l, int daysInMonth)
        {
            var vm = new HrTimesheetLineViewModel
            {
                ID = l.ID,
                TimesheetID = l.TimesheetID,
                EmployeeID = l.EmployeeID,
                EmployeeTabNo = l.HR_EMPLOYEES?.TabNo,
                EmployeeFullName = l.HR_EMPLOYEES?.FullName,
                PositionName = l.HR_EMPLOYEES?.HR_POSITIONS?.Name,
                ProductID = l.ProductID,
                ProductName = l.PRODUCT?.PNo,
                TotalWorkedDays = l.TotalWorkedDays ?? 0,
                TotalWorkedHours = l.TotalWorkedHours ?? 0,
                OvertimeHours = l.OvertimeHours ?? 0,
                AbsentDays = l.AbsentDays ?? 0,
                TotalQuantity = l.TotalQuantity ?? 0,
                IsPieceRate = false,
                DaysInMonth = daysInMonth
            };
            for (int d = 1; d <= MaxDays; d++)
            {
                VmHours[d].SetValue(vm, EntityHours[d].GetValue(l));
                VmQty[d].SetValue(vm, EntityQty[d].GetValue(l));
            }
            return vm;
        }
    }
}
