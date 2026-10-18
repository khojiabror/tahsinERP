# Database_Migrations (eski, 2026-10-18 gacha)

Bu papkadagi **sxema** skriptlari (Alter_*, Create_*, *_Indexes, *_Schema ...) lokal `DBTHSN-IPACK` da
qo'llangan va `Database/Schema/000_Baseline.sql` ga singdirilgan. Ular endi qo'lda ishlatilmaydi.

**Yangi sxema o'zgarishi bu yerga emas — `Database/Migrations/NNN_nomi.sql` ga** (qarang `Database/README.md`).

`DataFix_*`, `*_Check.sql`, `*_Find.sql`, `Tannarx_*_Fix.sql` — bitta bazaga tegishli ma'lumot tuzatishlari,
migratsiya emas; tarix uchun shu yerda qoladi. `Proposal_*` — taklif (Proposal_SRM_Procurement_Indexes →
`Database/Migrations/006`).
