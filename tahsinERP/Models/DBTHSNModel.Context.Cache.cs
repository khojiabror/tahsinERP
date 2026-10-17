namespace tahsinERP.Models
{
    using System;
    using System.Collections.Generic;
    using System.Data.Entity;
    using System.Data.Entity.Core.Objects;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using tahsinERP.Services.Common;

    /// <summary>
    /// SaveChanges'da spravochnik jadvallari o'zgarganini aniqlab, <see cref="ReferenceDataCache"/>ni
    /// avtomatik bekor qiladi — controllerlar keshni qo'lda tozalashni unutmasligi uchun.
    /// </summary>
    public partial class DBTHSNEntities
    {
        // DetectChanges bir marta chaqiriladi (o'zimiz), base.SaveChanges uni takrorlamasligi uchun
        // auto-detect vaqtincha o'chiriladi — katta importlarda qo'shimcha xarajat bo'lmaydi.
        public override int SaveChanges()
        {
            bool auto = Configuration.AutoDetectChangesEnabled;
            var changed = CollectReferenceChanges(auto);
            int result;
            try
            {
                Configuration.AutoDetectChangesEnabled = false;
                result = base.SaveChanges();
            }
            finally
            {
                Configuration.AutoDetectChangesEnabled = auto;
            }
            InvalidateReferenceCaches(changed);
            return result;
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
        {
            bool auto = Configuration.AutoDetectChangesEnabled;
            var changed = CollectReferenceChanges(auto);
            int result;
            try
            {
                Configuration.AutoDetectChangesEnabled = false;
                result = await base.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                Configuration.AutoDetectChangesEnabled = auto;
            }
            InvalidateReferenceCaches(changed);
            return result;
        }

        private List<Type> CollectReferenceChanges(bool detect)
        {
            if (detect)
                ChangeTracker.DetectChanges();

            bool auto = Configuration.AutoDetectChangesEnabled;
            Configuration.AutoDetectChangesEnabled = false;
            try
            {
                return ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .Select(e => ObjectContext.GetObjectType(e.Entity.GetType()))
                .Where(ReferenceDataCache.Tracks)
                .Distinct()
                .ToList();
            }
            finally
            {
                Configuration.AutoDetectChangesEnabled = auto;
            }
        }

        private static void InvalidateReferenceCaches(List<Type> changed)
        {
            foreach (var type in changed)
                ReferenceDataCache.InvalidateFor(type);
        }
    }
}
