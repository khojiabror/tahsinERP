using System;
using tahsinERP.Models;
using tahsinERP.Services.Common;

namespace tahsinERP.Services
{
    /// <summary>
    /// Logistika summalari uchun valyuta konvertatsiyasi (LOG-B13, foydalanuvchi qarori: CBU kursi, hujjat sanasi).
    ///  - Hisobot va balanslar so'mda: har hujjat o'z sanasidagi kurs bilan (<see cref="ToUzs"/>).
    ///  - Yo'l varaqasi bo'yicha to'langan — yo'l varaqasi valyutasida: boshqa valyutadagi to'lov
    ///    to'lov sanasidagi kurs bilan o'giriladi (<see cref="Convert"/>).
    /// Bitta sahifa/so'rov uchun bitta nusxa: kurslar keshlanadi, kurs topilmagan hujjatlar jamiga
    /// qo'shilmaydi va <see cref="Warning"/> orqali ko'rsatiladi. Asosiy valyutada bazaga murojaat yo'q.
    /// </summary>
    public sealed class LogisticsMoney
    {
        private readonly DBTHSNEntities _db;
        private UzsConverter _converter;

        public LogisticsMoney(DBTHSNEntities db)
        {
            _db = db;
        }

        private UzsConverter Converter => _converter ?? (_converter = new UzsConverter(_db));

        /// <summary>Kurs topilmagan bo'lsa foydalanuvchiga matn, aks holda null.</summary>
        public string Warning => _converter?.Warning;

        /// <summary>Summani so'mga o'giradi; kurs topilmasa 0 (jamiga qo'shilmaydi, ogohlantirish yoziladi).</summary>
        public double ToUzs(double amount, int? currencyId, DateTime? date)
        {
            if (amount == 0) return 0;
            if (!currencyId.HasValue || currencyId == Converter.BaseCurrencyId) return amount;
            return (double)(Converter.ToUzs((decimal)amount, currencyId, (date ?? DateTime.Today).Date) ?? 0m);
        }

        public double ToUzs(decimal amount, int? currencyId, DateTime? date) => ToUzs((double)amount, currencyId, date);

        /// <summary>
        /// Bir valyutadan boshqasiga (so'm orqali) sanadagi kurs bilan. Valyutalardan biri noma'lum yoki
        /// bir xil bo'lsa — o'zgarmaydi. Kurs topilmasa null.
        /// </summary>
        public double? Convert(double amount, int? fromCurrencyId, int? toCurrencyId, DateTime? date)
        {
            if (amount == 0 || !fromCurrencyId.HasValue || !toCurrencyId.HasValue || fromCurrencyId == toCurrencyId)
                return amount;

            var day = (date ?? DateTime.Today).Date;
            var uzs = Converter.ToUzs((decimal)amount, fromCurrencyId, day);
            if (!uzs.HasValue) return null;
            if (toCurrencyId == Converter.BaseCurrencyId) return (double)uzs.Value;

            var rate = Converter.ToUzs(1m, toCurrencyId, day);
            if (!rate.HasValue || rate.Value == 0) return null;
            return (double)(uzs.Value / rate.Value);
        }
    }
}
