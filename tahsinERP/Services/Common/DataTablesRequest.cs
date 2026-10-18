using System;
using System.Web;

namespace tahsinERP.Services.Common
{
    /// <summary>
    /// DataTables server-side (serverSide: true) so'rov parametrlari: draw, start, length,
    /// search[value], order[0][column], order[0][dir]. Katta jadvallarni (loglar, kirishlar)
    /// butunlay yuklamasdan sahifalab berish uchun.
    /// <code>
    /// var dt = DataTablesRequest.From(Request);
    /// var page = query.Skip(dt.Start).Take(dt.Length);
    /// return Json(dt.Response(total, filtered, rows), JsonRequestBehavior.AllowGet);
    /// </code>
    /// </summary>
    public sealed class DataTablesRequest
    {
        public const int MaxLength = 500;

        public int Draw { get; private set; }
        public int Start { get; private set; }
        public int Length { get; private set; }
        public string Search { get; private set; }
        public int OrderColumn { get; private set; }
        public bool OrderDesc { get; private set; }

        public static DataTablesRequest From(HttpRequestBase request)
        {
            var p = request.HttpMethod == "POST" ? request.Form : request.QueryString;
            int length = ParseInt(p["length"], 25);
            return new DataTablesRequest
            {
                Draw = ParseInt(p["draw"], 0),
                Start = Math.Max(0, ParseInt(p["start"], 0)),
                // -1 ("Barchasi") ham cheklanadi — to'liq ro'yxat Excel eksporti orqali
                Length = length <= 0 || length > MaxLength ? MaxLength : length,
                Search = (p["search[value]"] ?? "").Trim(),
                OrderColumn = ParseInt(p["order[0][column]"], -1),
                OrderDesc = !string.Equals(p["order[0][dir]"], "asc", StringComparison.OrdinalIgnoreCase)
            };
        }

        public object Response(int total, int filtered, object data)
        {
            return new { draw = Draw, recordsTotal = total, recordsFiltered = filtered, data };
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, out var n) ? n : fallback;
        }
    }
}
