using System;
using System.Data;
using System.Data.SqlClient;
using tahsinERP.Models;

namespace tahsinERP.Areas.Distribution.Data
{
    /// <summary>
    /// Distributsiya (<see cref="Dst"/>, Code-First) va mavjud ERP (<see cref="Erp"/>, EDMX — ombor FIFO,
    /// moliya posting servislari) kontekstlari BITTA SqlConnection va BITTA tranzaksiyada.
    /// Masalan, buyurtmani jo'natish: dst hujjati + FIFO chiqim + mijoz ledgeri — yoki hammasi, yoki hech biri.
    ///
    /// <code>
    /// using (var uow = new DistributionUnitOfWork(actor))
    /// {
    ///     ... uow.Dst.SaveChanges(); ... WarehouseFlowService.X(uow.Erp, ...); uow.Erp.SaveChanges();
    ///     uow.Commit();
    /// }   // Commit chaqirilmasa — Dispose'da rollback
    /// </code>
    /// </summary>
    public sealed class DistributionUnitOfWork : IDisposable
    {
        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;
        private bool _completed;
        private bool _disposed;

        public DistributionUnitOfWork(AuditActor actor = null, IsolationLevel isolation = IsolationLevel.ReadCommitted)
            : this(tahsinERP.Configuration.DbConfig.GetSqlConnectionString(), actor, isolation)
        {
        }

        public DistributionUnitOfWork(string sqlConnectionString, AuditActor actor, IsolationLevel isolation)
        {
            _connection = new SqlConnection(sqlConnectionString);
            try
            {
                _connection.Open();
                _transaction = _connection.BeginTransaction(isolation);

                Erp = DBTHSNEntities.CreateOnConnection(_connection);
                Erp.Database.UseTransaction(_transaction);

                Dst = new DistributionDbContext(_connection, false);
                if (actor != null) Dst.Actor = actor;
                Dst.Database.UseTransaction(_transaction);
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>Mavjud ERP modeli (EDMX) — shu tranzaksiyada.</summary>
        public DBTHSNEntities Erp { get; }

        /// <summary>Distributsiya konteksti — shu tranzaksiyada.</summary>
        public DistributionDbContext Dst { get; }

        /// <summary>Xom SQL kerak bo'lsa (masalan, sp_getapplock) — shu tranzaksiya.</summary>
        public SqlConnection Connection => _connection;
        public SqlTransaction Transaction => _transaction;

        public void Commit()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DistributionUnitOfWork));
            if (_completed) throw new InvalidOperationException("Tranzaksiya allaqachon yakunlangan.");
            _transaction.Commit();
            _completed = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try
            {
                if (!_completed && _transaction?.Connection != null)
                    _transaction.Rollback();
            }
            finally
            {
                Dst?.Dispose();
                Erp?.Dispose();
                _transaction?.Dispose();
                _connection.Dispose();
            }
        }
    }
}
