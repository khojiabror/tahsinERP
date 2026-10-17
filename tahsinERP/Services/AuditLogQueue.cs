using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Web.Hosting;
using tahsinERP.Models;

namespace tahsinERP.Services
{
    /// <summary>
    /// USERLOGS yozuvlarini so'rov oqimidan tashqarida, paketlab yozadi.
    /// Avval har bir LogHelper.LogToDatabase chaqiruvi sinxron: GetUserId so'rovi + DNS + yangi
    /// DbContext + INSERT edi (282 joyda, ko'pincha tranzaksiya ichida).
    ///
    /// Navbat har 2 soniyada yoki 100 ta yozuv yig'ilganda bazaga yoziladi. Ilova to'xtatilganda
    /// (IRegisteredObject) qolgan yozuvlar yozib bo'linadi. ASP.NET hosting bo'lmasa (testlar) — sinxron.
    /// </summary>
    public sealed class AuditLogQueue : IRegisteredObject
    {
        private const int BatchSize = 100;
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(2);

        private static readonly Lazy<AuditLogQueue> LazyInstance = new Lazy<AuditLogQueue>(() => new AuditLogQueue());
        public static AuditLogQueue Instance => LazyInstance.Value;

        private readonly ConcurrentQueue<USERLOG> _queue = new ConcurrentQueue<USERLOG>();
        private readonly Timer _timer;
        private readonly object _flushLock = new object();
        private readonly bool _hosted;

        private AuditLogQueue()
        {
            _hosted = HostingEnvironment.IsHosted;
            if (_hosted)
            {
                HostingEnvironment.RegisterObject(this);
                _timer = new Timer(_ => Flush(), null, FlushInterval, FlushInterval);
            }
        }

        public void Enqueue(USERLOG log)
        {
            if (log == null) return;

            _queue.Enqueue(log);
            if (!_hosted || _queue.Count >= BatchSize)
            {
                ThreadPool.QueueUserWorkItem(_ => Flush());
            }
        }

        /// <summary>Navbatdagi barcha yozuvlarni bazaga yozadi.</summary>
        public void Flush()
        {
            if (_queue.IsEmpty) return;
            if (!Monitor.TryEnter(_flushLock)) return; // boshqa oqim yozyapti
            try
            {
                while (!_queue.IsEmpty)
                {
                    var batch = new List<USERLOG>(BatchSize);
                    USERLOG item;
                    while (batch.Count < BatchSize && _queue.TryDequeue(out item))
                        batch.Add(item);

                    if (batch.Count == 0) break;
                    WriteBatch(batch);
                }
            }
            finally
            {
                Monitor.Exit(_flushLock);
            }
        }

        private static void WriteBatch(List<USERLOG> batch)
        {
            try
            {
                using (var db = new DBTHSNEntities())
                {
                    db.Configuration.AutoDetectChangesEnabled = false;
                    db.Configuration.ValidateOnSaveEnabled = false;
                    db.USERLOGS.AddRange(batch);
                    db.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                if (batch.Count == 1)
                {
                    // Log yozilmasligi ilovani to'xtatmasligi kerak
                    System.Diagnostics.Trace.TraceError("[AuditLog] Yozuvni saqlab bo'lmadi: " + ex.Message);
                    return;
                }

                // Bitta yaroqsiz yozuv butun paketni yo'qotmasligi uchun — birma-bir
                foreach (var log in batch)
                {
                    log.ID = 0;
                    WriteBatch(new List<USERLOG> { log });
                }
            }
        }

        public void Stop(bool immediate)
        {
            try
            {
                _timer?.Dispose();
                Flush();
            }
            finally
            {
                HostingEnvironment.UnregisterObject(this);
            }
        }
    }
}
