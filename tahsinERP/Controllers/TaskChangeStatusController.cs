using System;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;
using tahsinERP.Services.Tasks;
using tahsinERP.ViewModels;
using tahsinERP.I18n;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Topshiriq statusini o'zgartirish. Ruxsat: shu sahifani ko'rish huquqi (POST ham) —
    /// ijrochi o'z topshirig'i statusini Tasks tahrirlash huquqisiz ham o'zgartira oladi.
    /// Shu sabab [Permission] (POST = o'zgartirish) emas, ko'rish tekshiruvi qoldirilgan.
    /// </summary>
    public class TaskChangeStatusController : BaseController
    {
        private bool CanUsePage => RoleHelper.IsViewPermitted(CurrentUserName, "TaskChangeStatus", "ChangeStatus");

        // Olish so'rovi: TaskChangeStatus/ChangeStatus/5
        public async Task<ActionResult> ChangeStatus(long? id)
        {
            if (!CanUsePage)
            {
                return NotAuthorized();
            }

            if (id == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest);
            }

            using (DBTHSNEntities db = new DBTHSNEntities())
            {
                var task = await db.APP_TASKS.AsNoTracking()
                    .Include(t => t.APP_TASK_STATUS_REF)
                    .FirstOrDefaultAsync(t => t.ID == id && t.IsDeleted == false);

                if (task == null)
                {
                    return HttpNotFound();
                }

                // Oddiy user boshqalarning tasklarini ko'rmasligi kerak
                if (!TaskRules.CanAccess(task.AssigneeUserID, CurrentUserId, RoleHelper.IsAdminOrDeveloper(CurrentUserName)))
                {
                    TempData["Error"] = L.T("Task_OnlyOwnView");
                    return RedirectToAction("Index", "Tasks");
                }

                var viewModel = new TaskChangeStatusViewModel
                {
                    TaskID = task.ID,
                    KeyCode = task.KeyCode,
                    Title = task.Title,
                    CurrentStatusID = task.StatusID,
                    CurrentStatusName = task.APP_TASK_STATUS_REF?.Name,
                    UserComment = null
                };

                ViewBag.StatusID = TaskLookups.StatusList(task.StatusID);
                return View(viewModel);
            }
        }

        // POST so'rovi: TaskChangeStatus/ChangeStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> ChangeStatus(TaskChangeStatusViewModel viewModel)
        {
            if (!CanUsePage)
            {
                return NotAuthorized();
            }

            if (!TaskLookups.Statuses().Any(s => s.ID == viewModel.StatusID))
            {
                ModelState.AddModelError("StatusID", L.T("Task_InvalidStatus"));
            }

            if (ModelState.IsValid)
            {
                using (DBTHSNEntities db = new DBTHSNEntities())
                {
                    var task = await db.APP_TASKS.FindAsync(viewModel.TaskID);
                    if (task == null || task.IsDeleted)
                    {
                        return HttpNotFound();
                    }

                    // Oddiy user boshqalarning tasklarini o'zgartirmasligi kerak
                    if (!TaskRules.CanAccess(task.AssigneeUserID, CurrentUserId, RoleHelper.IsAdminOrDeveloper(CurrentUserName)))
                    {
                        TempData["Error"] = L.T("Task_OnlyOwnChange");
                        return RedirectToAction("Index", "Tasks");
                    }

                    try
                    {
                        task.StatusID = viewModel.StatusID;
                        task.UpdatedDT = DateTime.Now;

                        // UserComment EF modelida bor. Avval raw SQL bilan yozilib, keyin
                        // Entry.State = Modified + SaveChanges eski qiymatni qayta yozardi — izoh yo'qolardi (TSK-B01).
                        if (!string.IsNullOrWhiteSpace(viewModel.UserComment))
                        {
                            string entry = "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " - " + UserHelper.GetFullName(CurrentUserName) + "]:\n" + viewModel.UserComment;
                            task.UserComment = string.IsNullOrEmpty(task.UserComment)
                                ? entry
                                : task.UserComment + "\n\n" + entry;
                        }

                        await db.SaveChangesAsync();

                        string statusName = TaskLookups.Statuses().First(s => s.ID == task.StatusID).Name;
                        LogHelper.LogToDatabase(CurrentUserName, "TaskChangeStatusController",
                            $"Task {task.KeyCode} status o'zgartirildi: {statusName}");

                        TempData["Success"] = L.T("Task_StatusChanged");
                        return RedirectToAction("Details", "Tasks", new { id = task.ID });
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Trace.TraceError("TaskChangeStatus {0}: {1}", viewModel.TaskID, ex);
                        ModelState.AddModelError("", L.T("Task_SaveError", InnermostMessage(ex)));
                    }
                }
            }

            ViewBag.StatusID = TaskLookups.StatusList(viewModel.StatusID);
            return View(viewModel);
        }
    }
}
