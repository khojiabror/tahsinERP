using System;
using System.Collections.Generic;

namespace tahsinERP.Services
{
    public interface IDocumentTemplateService
    {
        /// <summary>Tanlangan shablonni ma'lumot bilan to'ldiradi. Shablon boshqa hujjat turiniki bo'lsa — istisno.</summary>
        GeneratedDocument Generate(int templateId, TemplateData data);
    }

    /// <summary>Shablonga qo'yiladigan ma'lumot (kalitlar — Services/Documents/DocPlaceholders katalogidan).</summary>
    public sealed class TemplateData
    {
        public TemplateData(string docTypeCode)
        {
            DocTypeCode = docTypeCode;
        }

        public string DocTypeCode { get; }

        /// <summary>Oddiy matn maydonlari (yangi qator — \n).</summary>
        public Dictionary<string, string> Values { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Formatlangan matn (Quill HTML) maydonlari.</summary>
        public Dictionary<string, string> Html { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Jadval maydonlari.</summary>
        public Dictionary<string, DocTable> Tables { get; } = new Dictionary<string, DocTable>(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class DocTable
    {
        public List<string> Headers { get; } = new List<string>();
        public List<List<string>> Rows { get; } = new List<List<string>>();

        /// <summary>O'ngga tekislanadigan (raqamli) ustunlar indekslari.</summary>
        public HashSet<int> NumericColumns { get; } = new HashSet<int>();

        /// <summary>Jadval shablonda matn ichida ishlatilsa — oddiy matn ko'rinishi.</summary>
        public string ToPlainText()
        {
            var lines = new List<string>();
            foreach (var row in Rows)
                lines.Add(string.Join(" | ", row));
            return string.Join("\n", lines);
        }
    }

    public sealed class GeneratedDocument
    {
        public byte[] Content { get; set; }
        public string FileExtension { get; set; }
        public string MimeType { get; set; }

        /// <summary>Shablondagi tanilmagan {{...}} maydonlar (o'zgarishsiz qoldi).</summary>
        public List<string> UnknownPlaceholders { get; } = new List<string>();
    }

    /// <summary>Shablon yuklashdagi tekshiruv natijasi.</summary>
    public sealed class TemplateScanResult
    {
        public bool IsValid { get; set; }
        public string Error { get; set; }
        public List<string> Recognized { get; } = new List<string>();
        public List<string> Unknown { get; } = new List<string>();
    }
}
