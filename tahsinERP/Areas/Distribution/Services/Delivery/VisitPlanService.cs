using System;
using System.Collections.Generic;
using System.Linq;
using tahsinERP.Areas.Distribution.Data;
using tahsinERP.Areas.Distribution.Data.Entities;

namespace tahsinERP.Areas.Distribution.Services.Delivery
{
    public sealed class PlanStop
    {
        public int CustomerID { get; set; }
        public int Seq { get; set; }
        public string Name { get; set; }
        public string Address { get; set; }
        public decimal? Lat { get; set; }
        public decimal? Lng { get; set; }
        public string Note { get; set; }
        /// <summary>Kunlik rejadan (true) yoki mijoz kartasidagi tashrif kunlaridan (false).</summary>
        public bool FromPlan { get; set; }
    }

    public sealed class PlanFactRow
    {
        public PlanStop Stop { get; set; }
        public int? VisitId { get; set; }
        public DateTime? CheckInAt { get; set; }
        public string Result { get; set; }
        public int? DistanceM { get; set; }
        public decimal? OrderTotal { get; set; }
        public bool Planned { get; set; }
    }

    /// <summary>
    /// Tashrif rejasi: mijoz kartasidagi tashrif kunlari (haftalik andoza) ustiga aniq kun uchun reja — tartib bilan, qo'shish/olib tashlash.
    /// Kunga reja saqlangan bo'lsa marshrut (bot), "rejadagi" tashrif belgisi va reja/fakt hisoboti shu rejadan; bo'lmasa — andozadan.
    /// </summary>
    public static class VisitPlanService
    {
        public const int MaxStops = 200;

        /// <summary>Kunga saqlangan reja bormi.</summary>
        public static bool HasPlan(DistributionDbContext db, int agentUserId, DateTime date)
        {
            var d = date.Date;
            return db.VisitPlans.Any(p => p.AgentUserID == agentUserId && p.PlanDate == d);
        }

        /// <summary>Kun rejasi: saqlangan reja (tartibda), bo'lmasa andoza (tashrif kuni bo'yicha agent mijozlari, nom bo'yicha).</summary>
        public static List<PlanStop> ForDay(DistributionDbContext db, int agentUserId, DateTime date)
        {
            var d = date.Date;
            var saved = (from p in db.VisitPlans
                         where p.AgentUserID == agentUserId && p.PlanDate == d
                         join c in db.Customers on p.CustomerID equals c.ID
                         let cp = db.CustomerProfiles.FirstOrDefault(x => x.CustomerID == p.CustomerID)
                         orderby p.Seq
                         select new PlanStop { CustomerID = p.CustomerID, Seq = p.Seq, Name = c.Name, Address = c.Address, Lat = cp.Lat, Lng = cp.Lng, Note = p.Note, FromPlan = true })
                .ToList();
            if (saved.Count > 0) return saved;

            byte bit = VisitDays.For(d.DayOfWeek);
            var template = (from p in db.CustomerProfiles
                            join c in db.Customers on p.CustomerID equals c.ID
                            where c.IsDeleted != true && p.AgentUserID == agentUserId && (p.VisitDays & bit) != 0
                            orderby c.Name
                            select new PlanStop { CustomerID = p.CustomerID, Name = c.Name, Address = c.Address, Lat = p.Lat, Lng = p.Lng }).ToList();
            for (int i = 0; i < template.Count; i++) template[i].Seq = i + 1;
            return template;
        }

        /// <summary>Kunga rejadagi mijozlar (marshrut uchun): saqlangan reja yoki andoza.</summary>
        public static HashSet<int> PlannedCustomers(DistributionDbContext db, int agentUserId, DateTime date) =>
            new HashSet<int>(ForDay(db, agentUserId, date).Select(s => s.CustomerID));

        /// <summary>
        /// Kun rejasini saqlash (to'liq almashtiriladi). Bo'sh ro'yxat — reja o'chadi, andozaga qaytiladi. Mijozlar agentning ko'rinishida
        /// bo'lishi shart emas (ofis boshqa hududdan ham qo'sha oladi), lekin distributsiya mijozi va o'chirilmagan bo'lsin.
        /// </summary>
        public static int Save(DistributionDbContext db, int agentUserId, DateTime date, IList<int> customerIds, int userId, DateTime now)
        {
            var d = date.Date;
            var ids = (customerIds ?? new List<int>()).Distinct().ToList();
            if (ids.Count > MaxStops) throw new DstValidationException(DstLocalization.T("Plan_TooMany", MaxStops));
            if (!db.SalesAgents.Any(a => a.UserID == agentUserId)) throw new DstValidationException(DstLocalization.T("Error_NotFound"));
            var valid = (from p in db.CustomerProfiles
                         join c in db.Customers on p.CustomerID equals c.ID
                         where ids.Contains(p.CustomerID) && c.IsDeleted != true
                         select p.CustomerID).ToList();
            if (valid.Count != ids.Count) throw new DstValidationException(DstLocalization.T("Error_NotFound"));

            var existing = db.VisitPlans.Where(p => p.AgentUserID == agentUserId && p.PlanDate == d).ToList();
            db.VisitPlans.RemoveRange(existing);
            db.SaveChanges();
            int seq = 0;
            foreach (var id in ids)
                db.VisitPlans.Add(new VisitPlan { AgentUserID = agentUserId, PlanDate = d, CustomerID = id, Seq = ++seq, CreatedBy = userId, CreatedAt = now });
            db.SaveChanges();
            return ids.Count;
        }

        /// <summary>Andozani ko'chirib, eng yaqin qo'shni tartibida saqlash (boshlanish — agentning o'sha kungi kun boshi joyi, bo'lmasa birinchi mijoz).</summary>
        public static decimal Optimize(DistributionDbContext db, int agentUserId, DateTime date, int userId, DateTime now)
        {
            var stops = ForDay(db, agentUserId, date);
            var d = date.Date;
            var start = db.AgentDays.Where(x => x.UserID == agentUserId && x.WorkDate == d).Select(x => new { x.StartLat, x.StartLng }).FirstOrDefault();
            var points = stops.Select(s => new GeoPoint { Id = s.CustomerID, Lat = s.Lat, Lng = s.Lng }).ToList();
            var order = DeliveryMath.NearestNeighbor(points, start?.StartLat, start?.StartLng);
            Save(db, agentUserId, date, order, userId, now);
            var byId = points.ToDictionary(p => p.Id);
            return DeliveryMath.PathKm(order.Select(id => byId[id]), start?.StartLat, start?.StartLng);
        }

        /// <summary>Kun bo'yicha reja/fakt: rejadagi har mijoz (tashrif bo'ldimi, natija) + rejadan tashqari tashriflar.</summary>
        public static List<PlanFactRow> PlanFact(DistributionDbContext db, int agentUserId, DateTime date)
        {
            var d = date.Date;
            var plan = ForDay(db, agentUserId, d);
            var visits = (from v in db.Visits
                          where v.UserID == agentUserId && v.VisitDate == d
                          join c in db.Customers on v.CustomerID equals c.ID
                          let cp = db.CustomerProfiles.FirstOrDefault(x => x.CustomerID == v.CustomerID)
                          orderby v.CheckInAt
                          select new
                          {
                              v.ID, v.CustomerID, v.CheckInAt, v.Result, v.DistanceM, c.Name, c.Address, cp.Lat, cp.Lng,
                              OrderTotal = db.Orders.Where(o => o.ID == v.OrderID && o.Status != DocumentStatus.Cancelled).Select(o => (decimal?)o.Total).FirstOrDefault()
                          }).ToList();
            var rows = plan.Select(p =>
            {
                var v = visits.FirstOrDefault(x => x.CustomerID == p.CustomerID);
                return new PlanFactRow
                {
                    Stop = p, Planned = true, VisitId = v?.ID, CheckInAt = v?.CheckInAt, Result = v?.Result, DistanceM = v?.DistanceM, OrderTotal = v?.OrderTotal
                };
            }).ToList();
            var planned = new HashSet<int>(plan.Select(p => p.CustomerID));
            rows.AddRange(visits.Where(v => !planned.Contains(v.CustomerID)).GroupBy(v => v.CustomerID).Select(g => g.First()).Select(v => new PlanFactRow
            {
                Stop = new PlanStop { CustomerID = v.CustomerID, Name = v.Name, Address = v.Address, Lat = v.Lat, Lng = v.Lng },
                Planned = false, VisitId = v.ID, CheckInAt = v.CheckInAt, Result = v.Result, DistanceM = v.DistanceM, OrderTotal = v.OrderTotal
            }));
            return rows;
        }
    }
}
