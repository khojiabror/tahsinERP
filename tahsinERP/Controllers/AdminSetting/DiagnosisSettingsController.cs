using System.Threading.Tasks;
using System.Web.Mvc;
using tahsinERP.Models;

namespace tahsinERP.Controllers
{
    /// <summary>
    /// Diagnosis sahifasi uchun AI sozlamalarining admin panel ko'rinishi.
    /// Hozircha faqat statik matnlarni ko'rsatadi, keyinroq DB orqali saqlash kengaytirilishi mumkin.
    /// </summary>
    public class DiagnosisSettingsController : Controller
    {
        // GET: DiagnosisSettings
        public async Task<ActionResult> Index()
        {
            if (!RoleHelper.IsViewPermitted(User.Identity.Name, RouteData.Values["controller"].ToString(), RouteData.Values["action"].ToString()))
            {
                return RedirectToAction("Index", "ViewNotAuthorized");
            }

            // Hozircha statik konfiguratsiya - keyinroq DB dan o'qish/sazhlashga kengaytirish mumkin
            var model = new DiagnosisAISettingsViewModel
            {
                SystemPrompt = "Sen tahsinERP bo‘yicha maslahatchisan.",
                ModulesPromptIntro = "Mana modullar ro‘yxati, har birining vazifasi va foydasi.",
                UserInputInstruction = "Foydalanuvchi quyidagi ma’lumotni kiritdi, sen 3–5 ta asosiy muammo va qaysi modul nima foyda berishini chiqar."
            };

            return View(model);
        }
    }

    /// <summary>
    /// Diagnosis sahifasi uchun AI prompt sozlamalari modeli.
    /// </summary>
    public class DiagnosisAISettingsViewModel
    {
        public string SystemPrompt { get; set; }
        public string ModulesPromptIntro { get; set; }
        public string UserInputInstruction { get; set; }
    }
}

