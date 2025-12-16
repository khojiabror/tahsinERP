using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
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

                // Use OpenXml SDK to properly handle DOCX files
                using (var templateStream = new MemoryStream(template.FileContent))
                using (var outputStream = new MemoryStream())
                {
                    // Copy template to output stream
                    templateStream.CopyTo(outputStream);
                    outputStream.Position = 0;

                    // Open the document using OpenXml SDK
                    using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(outputStream, true))
                    {
                        MainDocumentPart mainPart = wordDoc.MainDocumentPart;
                        if (mainPart == null)
                        {
                            throw new Exception("Invalid DOCX template: MainDocumentPart not found");
                        }

                        // Get the document body
                        Document document = mainPart.Document;
                        if (document == null || document.Body == null)
                        {
                            throw new Exception("Invalid DOCX template: Document body not found");
                        }

                        // Replace placeholders in all text elements
                        ReplacePlaceholdersInBody(document.Body, placeholders);

                        // Save changes
                        document.Save();
                    }

                    return outputStream.ToArray();
                }
            }
        }

        private void ReplacePlaceholdersInBody(Body body, IDictionary<string, string> placeholders)
        {
            if (body == null)
                return;

            // Process all paragraphs
            foreach (Paragraph paragraph in body.Elements<Paragraph>())
            {
                ReplacePlaceholdersInParagraph(paragraph, placeholders);
            }

            // Process tables
            foreach (Table table in body.Elements<Table>())
            {
                foreach (TableRow row in table.Elements<TableRow>())
                {
                    foreach (TableCell cell in row.Elements<TableCell>())
                    {
                        foreach (Paragraph para in cell.Elements<Paragraph>())
                        {
                            ReplacePlaceholdersInParagraph(para, placeholders);
                        }
                    }
                }
            }
        }

        private void ReplacePlaceholdersInParagraph(Paragraph paragraph, IDictionary<string, string> placeholders)
        {
            if (paragraph == null)
                return;

            // Collect all text from all runs in the paragraph
            var runs = paragraph.Elements<Run>().ToList();
            if (runs.Count == 0)
                return;

            // Build full text from all runs
            StringBuilder fullText = new StringBuilder();
            var textElements = new List<Text>();

            foreach (var run in runs)
            {
                foreach (var text in run.Elements<Text>())
                {
                    if (text.Text != null)
                    {
                        fullText.Append(text.Text);
                        textElements.Add(text);
                    }
                }
            }

            string combinedText = fullText.ToString();
            bool hasPlaceholder = false;

            // Check if any placeholder exists
            foreach (var placeholder in placeholders)
            {
                string placeholderKey = "{{" + placeholder.Key + "}}";
                if (combinedText.Contains(placeholderKey))
                {
                    hasPlaceholder = true;
                    break;
                }
            }

            if (!hasPlaceholder)
                return;

            // Replace all placeholders
            string replacedText = combinedText;
            foreach (var placeholder in placeholders)
            {
                string placeholderKey = "{{" + placeholder.Key + "}}";
                string value = placeholder.Value ?? string.Empty;
                replacedText = replacedText.Replace(placeholderKey, value);
            }

            // If text changed, update the paragraph
            if (replacedText != combinedText)
            {
                // Clear all runs
                paragraph.RemoveAllChildren<Run>();

                // Split by newlines and create new runs
                string[] lines = replacedText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                
                // Get formatting from first run if available
                RunProperties runProperties = null;
                if (runs.Count > 0)
                {
                    runProperties = runs[0].RunProperties?.Clone() as RunProperties;
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    Run newRun = new Run();
                    
                    // Copy formatting if available
                    if (runProperties != null)
                    {
                        newRun.RunProperties = runProperties.Clone() as RunProperties;
                    }

                    newRun.AppendChild(new Text(lines[i]));

                    // Add line break after each line except the last
                    if (i < lines.Length - 1)
                    {
                        newRun.AppendChild(new Break());
                    }

                    paragraph.AppendChild(newRun);
                }
            }
        }
    }
}
