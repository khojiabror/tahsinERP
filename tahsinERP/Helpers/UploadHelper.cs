using System.IO;
using System.Web;

namespace tahsinERP.Helpers
{
    /// <summary>
    /// Yuklangan faylni to'liq o'qish. Avval "new byte[Length]; InputStream.Read(...)" ko'chirilgan edi —
    /// Stream.Read bir chaqiruvda hamma baytni o'qishi kafolatlanmagan (katta rasm qisman saqlanardi).
    /// </summary>
    public static class UploadHelper
    {
        public static byte[] ReadAll(HttpPostedFileBase file)
        {
            if (file == null || file.ContentLength <= 0)
                return null;

            file.InputStream.Position = 0;
            using (var ms = new MemoryStream(file.ContentLength))
            {
                file.InputStream.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
