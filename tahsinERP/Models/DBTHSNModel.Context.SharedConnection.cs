namespace tahsinERP.Models
{
    using System;
    using System.Data.Common;
    using System.Data.Entity.Core.EntityClient;
    using System.Data.Entity.Core.Metadata.Edm;

    /// <summary>
    /// Mavjud (ochiq) SqlConnection ustida kontekst — boshqa kontekst (masalan, distributsiyaning
    /// Code-First <c>DistributionDbContext</c>) bilan bitta tranzaksiyada ishlash uchun.
    /// </summary>
    public partial class DBTHSNEntities
    {
        // EDMX metadata'ni har safar yuklamaslik uchun (qimmat) — ilova bo'yicha bitta
        private static readonly Lazy<MetadataWorkspace> SharedWorkspace = new Lazy<MetadataWorkspace>(() =>
            new MetadataWorkspace(
                tahsinERP.Configuration.DbConfig.EntityMetadata.Split('|'),
                new[] { typeof(DBTHSNEntities).Assembly }));

        private DBTHSNEntities(DbConnection existingConnection, bool contextOwnsConnection)
            : base(existingConnection, contextOwnsConnection)
        {
        }

        /// <summary>
        /// <paramref name="storeConnection"/> ustida kontekst yaratadi; ulanish egasi chaqiruvchi
        /// (kontekst uni yopmaydi). Tranzaksiya: <c>Database.UseTransaction(tx)</c>.
        /// </summary>
        public static DBTHSNEntities CreateOnConnection(DbConnection storeConnection)
        {
            if (storeConnection == null) throw new ArgumentNullException(nameof(storeConnection));
            var entityConnection = new EntityConnection(SharedWorkspace.Value, storeConnection, false);
            return new DBTHSNEntities(entityConnection, true);
        }
    }
}
