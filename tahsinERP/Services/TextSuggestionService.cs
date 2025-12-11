using System;
using System.Linq;
using System.Text;

namespace tahsinERP.Services
{
    public class TextSuggestionService : ITextSuggestionService
    {
        public string SuggestOrderBody(CompanyOrderMeta meta)
        {
            var sb = new StringBuilder();
            
            // Build Uzbek text based on order type and metadata
            sb.AppendLine("Ushbu buyruq asosida quyidagilar amalga oshiriladi:");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(meta.Topic))
            {
                sb.AppendLine(meta.Topic);
                sb.AppendLine();
            }
            
            if (meta.ResponsibleEmployeeNames != null && meta.ResponsibleEmployeeNames.Any())
            {
                sb.AppendLine("Javobgar shaxslar:");
                foreach (var emp in meta.ResponsibleEmployeeNames)
                {
                    sb.AppendLine($"- {emp}");
                }
                sb.AppendLine();
            }
            
            if (!string.IsNullOrEmpty(meta.OrgUnitName))
            {
                sb.AppendLine($"Tashkilot bo'limi: {meta.OrgUnitName}");
                sb.AppendLine();
            }
            
            sb.AppendLine($"Buyruq sanasi: {meta.OrderDate:dd.MM.yyyy}");
            sb.AppendLine();
            sb.AppendLine("Buyruqni bajarish muddati: 30 kun");
            
            return sb.ToString();
        }

        public string SuggestLetterBody(OfficialLetterMeta meta)
        {
            var sb = new StringBuilder();
            
            // Build polite commercial Uzbek letter
            sb.AppendLine("Hurmatli " + (string.IsNullOrEmpty(meta.ExternalContactName) ? "hamkor" : meta.ExternalContactName) + "!");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(meta.Topic))
            {
                sb.AppendLine(meta.Topic);
                sb.AppendLine();
            }
            
            sb.AppendLine("Ushbu maktub orqali sizga quyidagi ma'lumotlarni yetkazmoqdamiz:");
            sb.AppendLine();
            sb.AppendLine("Bizning kompaniyamiz yuqori sifatli mahsulotlar va xizmatlarni taklif etmoqda.");
            sb.AppendLine("Batafsil ma'lumot uchun biz bilan bog'laning.");
            sb.AppendLine();
            
            if (!string.IsNullOrEmpty(meta.OurRefNo))
            {
                sb.AppendLine($"Bizning havola raqami: {meta.OurRefNo}");
            }
            
            if (!string.IsNullOrEmpty(meta.TheirRefNo))
            {
                sb.AppendLine($"Sizning havola raqamingiz: {meta.TheirRefNo}");
            }
            
            sb.AppendLine();
            sb.AppendLine("Hurmat bilan,");
            sb.AppendLine("Direktor");
            
            return sb.ToString();
        }
    }
}

