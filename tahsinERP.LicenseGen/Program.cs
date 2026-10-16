using System;
using System.Windows.Forms;

namespace tahsinERP.LicenseGen
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 1) Master parol orqali private key'ni ochish (yoki birinchi marta import qilish)
            string privateKeyXml;
            using (var pwForm = new MasterPasswordForm())
            {
                if (pwForm.ShowDialog() != DialogResult.OK)
                    return; // bekor qilindi -> chiqish

                privateKeyXml = pwForm.PrivateKeyXml;
            }

            if (string.IsNullOrWhiteSpace(privateKeyXml))
                return;

            // 2) Asosiy oyna
            Application.Run(new MainForm(privateKeyXml));
        }
    }
}
