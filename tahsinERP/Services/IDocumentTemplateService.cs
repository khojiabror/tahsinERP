using System.Collections.Generic;

namespace tahsinERP.Services
{
    public interface IDocumentTemplateService
    {
        byte[] GenerateDocFromTemplate(
            int templateId,
            IDictionary<string, string> placeholders,
            out string fileName,
            out string fileExtension,
            out string mimeType);
    }
}

