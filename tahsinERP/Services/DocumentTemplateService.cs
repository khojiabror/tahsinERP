using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    public class DocumentTemplateService : IDocumentTemplateService
    {
        public byte[] GenerateDocFromTemplate(
            int templateId,
            IDictionary<string, string> placeholders,
            out string fileName,
            out string fileExtension,
            out string mimeType)
        {
            using (var db = new DBTHSNEntities())
            {
                var template = db.DOC_TEMPLATES.FirstOrDefault(t => t.ID == templateId && t.IsActive);
                if (template == null || template.FileContent == null)
                {
                    throw new Exception($"Template with ID {templateId} not found or inactive");
                }

                fileName = template.FileName ?? "document";
                fileExtension = template.FileExtension ?? ".docx";
                mimeType = template.MimeType ?? "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

                // DOCX files are ZIP archives containing XML files
                // We need to extract, modify document.xml, and repackage
                using (var templateStream = new MemoryStream(template.FileContent))
                using (var outputStream = new MemoryStream())
                {
                    using (var archive = new ZipArchive(templateStream, ZipArchiveMode.Read))
                    using (var outputArchive = new ZipArchive(outputStream, ZipArchiveMode.Create))
                    {
                        // Copy all entries from template to output
                        foreach (var entry in archive.Entries)
                        {
                            var newEntry = outputArchive.CreateEntry(entry.FullName);
                            
                            using (var entryStream = entry.Open())
                            using (var newEntryStream = newEntry.Open())
                            {
                                // If this is the main document.xml, replace placeholders
                                if (entry.FullName == "word/document.xml")
                                {
                                    using (var reader = new StreamReader(entryStream, Encoding.UTF8))
                                    {
                                        string xmlContent = reader.ReadToEnd();
                                        
                                        // Replace all placeholders
                                        foreach (var placeholder in placeholders)
                                        {
                                            string placeholderKey = "{{" + placeholder.Key + "}}";
                                            string value = placeholder.Value ?? string.Empty;
                                            // Escape XML special characters
                                            value = System.Security.SecurityElement.Escape(value);
                                            xmlContent = xmlContent.Replace(placeholderKey, value);
                                        }
                                        
                                        // Write modified content
                                        byte[] xmlBytes = Encoding.UTF8.GetBytes(xmlContent);
                                        newEntryStream.Write(xmlBytes, 0, xmlBytes.Length);
                                    }
                                }
                                else
                                {
                                    // Copy other files as-is
                                    entryStream.CopyTo(newEntryStream);
                                }
                            }
                        }
                    }
                    
                    return outputStream.ToArray();
                }
            }
        }
    }
}

