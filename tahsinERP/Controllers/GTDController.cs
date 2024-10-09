using DocumentFormat.OpenXml.Bibliography;
using System;
using System.Collections.Generic;
using System.Data.Entity.Infrastructure;
using System.Data.SqlClient;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    public class GTDController : Controller
    {
        public ActionResult Index()
        {
            using ( DBTHSNEntities db = new DBTHSNEntities())
            {
                /*var list = db.GTDs.Where(x => x.IsDeleted == false).ToList();
                return View(list);*/
            }
            return View();
        }
        public ActionResult Create()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {

            }
            return View();
        }
        public ActionResult Delete()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {

            }
            return View();
        }
        public ActionResult Details()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {

            }
            return View();
        }
        public ActionResult Edit()
        {
            using (DBTHSNEntities db = new DBTHSNEntities())
            {

            }
            return View();
        }

    }
}