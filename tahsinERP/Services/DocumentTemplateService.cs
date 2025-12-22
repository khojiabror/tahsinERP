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

                // Create a copy of template content
                byte[] templateBytes = new byte[template.FileContent.Length];
                Array.Copy(template.FileContent, templateBytes, template.FileContent.Length);
                
                using (var stream = new MemoryStream())
                {
                    stream.Write(templateBytes, 0, templateBytes.Length);
                    stream.Position = 0;

                    // Open document for editing
                    using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(stream, true))
                    {
                        MainDocumentPart mainPart = wordDoc.MainDocumentPart;
                        if (mainPart == null)
                        {
                            throw new Exception("Invalid DOCX template: MainDocumentPart not found");
                        }

                        Document document = mainPart.Document;
                        if (document == null || document.Body == null)
                        {
                            throw new Exception("Invalid DOCX template: Document body not found");
                        }

                        // Replace placeholders while preserving formatting and structure
                        ReplacePlaceholdersPreservingFormat(document.Body, placeholders);

                        // Save the document
                        document.Save();
                    }

                    // Return the modified content
                    return stream.ToArray();
                }
            }
        }

        private void ReplacePlaceholdersPreservingFormat(OpenXmlElement element, IDictionary<string, string> placeholders)
        {
            if (element == null)
                return;

            // Process all paragraphs (including those in tables)
            var paragraphs = element.Descendants<Paragraph>().ToList();
            foreach (var paragraph in paragraphs)
            {
                ReplaceInParagraph(paragraph, placeholders);
            }
        }

        private void ReplaceInParagraph(Paragraph paragraph, IDictionary<string, string> placeholders)
        {
            if (paragraph == null)
                return;

            // Get all runs in the paragraph
            var runs = paragraph.Elements<Run>().ToList();
            if (runs.Count == 0)
                return;

            // Strategy: Build full text, replace placeholders, then redistribute back to runs
            // This preserves Run structure and formatting
            
            // Step 1: Collect all text from all runs
            StringBuilder fullText = new StringBuilder();
            var runInfo = new List<RunInfo>();
            
            foreach (var run in runs)
            {
                var texts = run.Elements<Text>().ToList();
                int startPos = fullText.Length;
                int length = 0;
                
                foreach (var text in texts)
                {
                    if (text.Text != null)
                    {
                        fullText.Append(text.Text);
                        length += text.Text.Length;
                    }
                }
                
                if (length > 0)
                {
                    runInfo.Add(new RunInfo
                    {
                        Run = run,
                        StartPos = startPos,
                        Length = length,
                        TextElements = texts
                    });
                }
            }

            string combinedText = fullText.ToString();
            
            // Step 2: Check if any placeholder exists
            bool hasPlaceholder = combinedText.Contains("{{");
            if (!hasPlaceholder)
                return;

            // Step 3: Replace all placeholders
            string replacedText = ReplaceAllPlaceholderFormats(combinedText, placeholders);
            
            if (replacedText == combinedText)
                return; // No changes

            // Step 4: Redistribute replaced text back to runs, preserving formatting
            // If replaced text is much longer than original, we need to handle it differently
            if (replacedText.Length > combinedText.Length * 2)
            {
                // Text is significantly longer - rebuild paragraph completely but preserve formatting
                var firstRun = runs.FirstOrDefault();
                RunProperties runProps = firstRun?.RunProperties?.Clone() as RunProperties;
                
                // Clear all runs
                paragraph.RemoveAllChildren<Run>();
                
                // Create new run with all text, preserving formatting
                Run newRun = new Run();
                if (runProps != null)
                {
                    newRun.RunProperties = runProps;
                }
                
                // Split by newlines and create text with breaks
                string[] lines = replacedText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                for (int i = 0; i < lines.Length; i++)
                {
                    newRun.AppendChild(new Text(lines[i]));
                    if (i < lines.Length - 1)
                    {
                        newRun.AppendChild(new Break());
                    }
                }
                
                paragraph.AppendChild(newRun);
            }
            else
            {
                // Normal case - redistribute text to existing runs
                int currentPos = 0;
                
                foreach (var info in runInfo)
                {
                    if (currentPos >= replacedText.Length)
                    {
                        // Remove all text from this run
                        foreach (var text in info.TextElements)
                        {
                            text.Text = "";
                        }
                        continue;
                    }
                    
                    // Calculate how much text should go to this run
                    int textToAssign = Math.Min(info.Length, replacedText.Length - currentPos);
                    string textForRun = replacedText.Substring(currentPos, textToAssign);
                    
                    // Update the first text element, clear others
                    if (info.TextElements.Count > 0)
                    {
                        info.TextElements[0].Text = textForRun;
                        for (int i = 1; i < info.TextElements.Count; i++)
                        {
                            info.TextElements[i].Text = "";
                        }
                    }
                    else if (textForRun.Length > 0)
                    {
                        // Create new text element if none exists
                        info.Run.AppendChild(new Text(textForRun));
                    }
                    
                    currentPos += textToAssign;
                }
                
                // Step 5: If there's remaining text, add it to the last run or create new runs
                if (currentPos < replacedText.Length)
                {
                    string remainingText = replacedText.Substring(currentPos);
                    if (remainingText.Length > 0)
                    {
                        // Split remaining text by newlines
                        string[] remainingLines = remainingText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        
                        var firstRun = runs.FirstOrDefault();
                        RunProperties runProps = firstRun?.RunProperties?.Clone() as RunProperties;
                        
                        foreach (string line in remainingLines)
                        {
                            if (string.IsNullOrEmpty(line))
                                continue;
                                
                            Run newRun = new Run();
                            if (runProps != null)
                            {
                                newRun.RunProperties = runProps.Clone() as RunProperties;
                            }
                            newRun.AppendChild(new Text(line));
                            paragraph.AppendChild(newRun);
                            
                            // Add break after each line except the last
                            if (line != remainingLines.Last())
                            {
                                Run breakRun = new Run();
                                if (runProps != null)
                                {
                                    breakRun.RunProperties = runProps.Clone() as RunProperties;
                                }
                                breakRun.AppendChild(new Break());
                                paragraph.AppendChild(breakRun);
                            }
                        }
                    }
                }
            }
        }

        private class RunInfo
        {
            public Run Run { get; set; }
            public int StartPos { get; set; }
            public int Length { get; set; }
            public List<Text> TextElements { get; set; }
        }

        private string ReplaceAllPlaceholderFormats(string text, IDictionary<string, string> placeholders)
        {
            string result = text;

            // First, replace nested placeholder formats (from template)
            var nestedMappings = new Dictionary<string, string>
            {
                { "{{Letter.No}}", placeholders.ContainsKey("LETTER_NO") ? placeholders["LETTER_NO"] : "" },
                { "{{Letter.Date}}", placeholders.ContainsKey("LETTER_DATE") ? placeholders["LETTER_DATE"] : "" },
                { "{{Receipent.Receiver.Name}}", placeholders.ContainsKey("GREETING_LINE") ? placeholders["GREETING_LINE"] : (placeholders.ContainsKey("RECIPIENT_BLOCK") ? placeholders["RECIPIENT_BLOCK"] : "") },
                { "{{Receipent.CompanyName}}", placeholders.ContainsKey("RECIPIENT_BLOCK") ? placeholders["RECIPIENT_BLOCK"] : "" },
                { "{{Receipent.Company.Receiver.Position}}", placeholders.ContainsKey("RECIPIENT_BLOCK") ? placeholders["RECIPIENT_BLOCK"] : "" },
                { "{{Content}}", placeholders.ContainsKey("LETTER_BODY") ? placeholders["LETTER_BODY"] : "" },
                // Company Orders placeholders
                { "{{ORDER_NO}}", placeholders.ContainsKey("ORDER_NO") ? placeholders["ORDER_NO"] : (placeholders.ContainsKey("ORDER NO") ? placeholders["ORDER NO"] : "") },
                { "{{ORDER NO}}", placeholders.ContainsKey("ORDER_NO") ? placeholders["ORDER_NO"] : (placeholders.ContainsKey("ORDER NO") ? placeholders["ORDER NO"] : "") }
            };

            // Replace nested format placeholders
            foreach (var mapping in nestedMappings)
            {
                if (result.Contains(mapping.Key))
                {
                    result = result.Replace(mapping.Key, mapping.Value);
                }
            }

            // Then replace standard format placeholders ({{KEY}})
            foreach (var kvp in placeholders)
            {
                string placeholderKey = "{{" + kvp.Key + "}}";
                string value = kvp.Value ?? string.Empty;
                
                if (result.Contains(placeholderKey))
                {
                    result = result.Replace(placeholderKey, value);
                }
            }

            return result;
        }
    }
}
