using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using tahsinERP.Models;

namespace tahsinERP.Services.Documents
{
    /// <summary>
    /// Xat va buyruq ma'lumotini shablon maydonlariga (DocPlaceholders) aylantiradi.
    /// Avval bu kod OfficialLetters/CompanyOrders controllerlarida edi.
    /// </summary>
    public static class DocTemplateDataBuilder
    {
        private const string Money = "#,0.##";

        public static TemplateData ForLetter(DOC_DOCUMENTS doc, COM_OFFICIAL_LETTERS letter, OfficialLetterData letterData, string directorName)
        {
            var data = new TemplateData(DocPlaceholders.LetterType);
            var v = data.Values;

            var recipient = new List<string>();
            if (!string.IsNullOrWhiteSpace(letter.ExternalCompanyName)) recipient.Add(letter.ExternalCompanyName.Trim());
            if (!string.IsNullOrWhiteSpace(letter.ExternalContactPosition)) recipient.Add(letter.ExternalContactPosition.Trim());
            if (!string.IsNullOrWhiteSpace(letter.ExternalContactName)) recipient.Add(letter.ExternalContactName.Trim());

            v["LETTER_NO"] = letter.LetterNo;
            v["LETTER_DATE"] = letter.LetterDate.ToString("dd.MM.yyyy");
            v["COMPANY_NAME"] = letter.ExternalCompanyName ?? "";
            v["CONTACT_NAME"] = letter.ExternalContactName ?? "";
            v["CONTACT_POSITION"] = letter.ExternalContactPosition ?? "";
            v["RECIPIENT_BLOCK"] = string.Join("\n", recipient);
            v["GREETING_LINE"] = !string.IsNullOrWhiteSpace(letterData.GreetingLine)
                ? letterData.GreetingLine
                : $"Hurmatli {letter.ExternalContactName ?? "hamkor"}!";
            v["TOPIC"] = letter.Topic ?? "";
            v["OUR_REF_NO"] = letter.OurRefNo ?? "";
            v["THEIR_REF_NO"] = letter.TheirRefNo ?? "";
            v["CHANNEL"] = letter.ChannelCode ?? "";
            AddCommon(data, directorName);

            if (!string.IsNullOrWhiteSpace(letterData.BodyHtml))
                data.Html["LETTER_BODY"] = letterData.BodyHtml;
            else
                v["LETTER_BODY"] = doc.Title ?? "";

            var items = letterData.Items ?? new List<OfficialLetterItemData>();
            v["ITEMS_BLOCK"] = ItemsText(items);
            data.Tables["ITEMS_TABLE"] = ItemsTable(items);

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                int n = i + 1;
                v["ITEM_NAME_" + n] = it.ItemName ?? "";
                v["ITEM_DIM_" + n] = it.ItemSize ?? "";
                v["ITEM_PRICE_EXW_" + n] = Format(it.PriceExw);
                v["ITEM_PRICE_VAT_EXW_" + n] = Format(it.PriceVatExw);
                v["ITEM_PRICE_CIP_" + n] = Format(it.PriceCip);
                v["ITEM_PRICE_VAT_CIP_" + n] = Format(it.PriceVatCip);
            }

            return data;
        }

        public static TemplateData ForOrder(DOC_DOCUMENTS doc, ADM_COMPANY_ORDERS order, string orgUnitName, string directorName)
        {
            var data = new TemplateData(DocPlaceholders.OrderType);
            var v = data.Values;

            v["ORDER_NO"] = order.OrderNo;
            v["ORDER_DATE"] = order.OrderDate.ToString("dd.MM.yyyy");
            v["ORDER_TOPIC"] = doc.Title ?? "";
            v["ORDER_TYPE"] = order.OrderTypeCode ?? "";
            v["ORG_UNIT_NAME"] = orgUnitName ?? "";
            AddCommon(data, directorName);

            string html = ToHtml(order.Notes);
            if (!string.IsNullOrWhiteSpace(html))
                data.Html["ORDER_BODY"] = html;
            else
                v["ORDER_BODY"] = "";

            // Bandlar (ORDER_DECREE_1_CONTENT_N): bo'sh bo'lmagan qatorlar, boshidagi "1." / "1)" raqamsiz
            var lines = OfficialLetterDataService.HtmlToPlainText(order.Notes)
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => Regex.Replace(l, @"^\s*(\d+[\.\)\-]\s*|•\s*)", "").Trim())
                .Where(l => l.Length > 0)
                .ToList();
            for (int i = 0; i < lines.Count; i++)
                v["ORDER_DECREE_1_CONTENT_" + (i + 1)] = lines[i];

            return data;
        }

        private static void AddCommon(TemplateData data, string directorName)
        {
            data.Values["DIRECTOR_NAME"] = directorName ?? "";
            data.Values["TODAY"] = DateTime.Now.ToString("dd.MM.yyyy");
        }

        /// <summary>Eski buyruqlarda Notes oddiy matn — qatorlarni paragrafga aylantiramiz.</summary>
        private static string ToHtml(string notes)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return null;
            if (Regex.IsMatch(notes, @"<\s*(p|div|ol|ul|li|br|h[1-6]|strong|em|span)\b", RegexOptions.IgnoreCase))
                return notes;

            var sb = new StringBuilder();
            foreach (var line in notes.Replace("\r", "").Split('\n'))
                sb.Append("<p>").Append(line.Length == 0 ? "<br>" : WebUtility.HtmlEncode(line)).Append("</p>");
            return sb.ToString();
        }

        private static string Format(decimal? value) => value.HasValue ? value.Value.ToString(Money) : "";

        private static string ItemsText(List<OfficialLetterItemData> items)
        {
            var sb = new StringBuilder();
            int i = 1;
            foreach (var it in items)
            {
                sb.Append(i++ + ". " + (it.ItemName ?? ""));
                if (!string.IsNullOrWhiteSpace(it.ItemSize))
                    sb.Append(" (" + it.ItemSize + ")");
                sb.Append('\n');
                if (it.PriceExw.HasValue) sb.Append("   Narx EXW: " + Format(it.PriceExw) + "\n");
                if (it.PriceVatExw.HasValue) sb.Append("   Narx QQS bilan EXW: " + Format(it.PriceVatExw) + "\n");
                if (it.PriceCip.HasValue) sb.Append("   Narx CIP: " + Format(it.PriceCip) + "\n");
                if (it.PriceVatCip.HasValue) sb.Append("   Narx QQS bilan CIP: " + Format(it.PriceVatCip) + "\n");
            }
            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>Faqat qiymati bor ustunlar bilan jadval (bo'sh narx ustunlari chiqmaydi).</summary>
        private static DocTable ItemsTable(List<OfficialLetterItemData> items)
        {
            var table = new DocTable();
            if (items.Count == 0)
                return table;

            var columns = new List<Tuple<string, Func<OfficialLetterItemData, string>, bool>>
            {
                Tuple.Create<string, Func<OfficialLetterItemData, string>, bool>("Nomi", it => it.ItemName ?? "", false)
            };
            if (items.Any(it => !string.IsNullOrWhiteSpace(it.ItemSize)))
                columns.Add(Tuple.Create<string, Func<OfficialLetterItemData, string>, bool>("O'lchami", it => it.ItemSize ?? "", false));
            if (items.Any(it => it.PriceExw.HasValue))
                columns.Add(Tuple.Create<string, Func<OfficialLetterItemData, string>, bool>("Narx EXW", it => Format(it.PriceExw), true));
            if (items.Any(it => it.PriceVatExw.HasValue))
                columns.Add(Tuple.Create<string, Func<OfficialLetterItemData, string>, bool>("QQS bilan EXW", it => Format(it.PriceVatExw), true));
            if (items.Any(it => it.PriceCip.HasValue))
                columns.Add(Tuple.Create<string, Func<OfficialLetterItemData, string>, bool>("Narx CIP", it => Format(it.PriceCip), true));
            if (items.Any(it => it.PriceVatCip.HasValue))
                columns.Add(Tuple.Create<string, Func<OfficialLetterItemData, string>, bool>("QQS bilan CIP", it => Format(it.PriceVatCip), true));

            table.Headers.Add("№");
            table.Headers.AddRange(columns.Select(c => c.Item1));
            for (int c = 0; c < columns.Count; c++)
            {
                if (columns[c].Item3)
                    table.NumericColumns.Add(c + 1);
            }

            int n = 1;
            foreach (var it in items)
            {
                var row = new List<string> { (n++).ToString() };
                row.AddRange(columns.Select(c => c.Item2(it)));
                table.Rows.Add(row);
            }
            return table;
        }
    }
}
