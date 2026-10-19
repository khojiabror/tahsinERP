namespace tahsinERP.Services.Common
{
    /// <summary>
    /// decimal miqdorni hujjat raqami/izoh matnida avvalgi double ko'rinishida chiqarish: ortiqcha nollarsiz
    /// (bazadagi decimal(18,6) "5.000000" emas, "5"), joriy madaniyat ajratgichi bilan — double.ToString() kabi.
    /// </summary>
    public static class QtyText
    {
        public static string Plain(decimal value)
        {
            return value.ToString("0.############################");
        }
    }
}
