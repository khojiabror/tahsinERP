using System;
using System.Drawing;
using System.Windows.Forms;

namespace tahsinERP.LicenseGen
{
    /// <summary>
    /// Master parol oynasi.
    ///  - Birinchi ishga tushirish: private key XML kiritiladi + master parol o'rnatiladi (shifrlanib saqlanadi).
    ///  - Keyingi safar: faqat master parol so'raladi -> private key ochiladi.
    /// </summary>
    public class MasterPasswordForm : Form
    {
        public string PrivateKeyXml { get; private set; }

        private readonly bool _firstRun;
        private TextBox _txtPassword;
        private TextBox _txtPassword2;
        private TextBox _txtPrivateKey;
        private Label _lblInfo;
        private Button _btnOk;

        public MasterPasswordForm()
        {
            _firstRun = !PrivateKeyStore.Exists();
            BuildUi();
        }

        private void BuildUi()
        {
            Text = _firstRun ? "Birinchi sozlash — private key import" : "Master parol";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 560;
            Height = _firstRun ? 430 : 200;
            Font = new Font("Segoe UI", 9f);

            _lblInfo = new Label
            {
                Left = 15, Top = 12, Width = 515, Height = 40,
                Text = _firstRun
                    ? "Birinchi ishga tushirish. RSA PRIVATE key XML'ni joylashtiring va master parol o'rnating. Kalit shifrlanib saqlanadi — parolsiz ochilmaydi."
                    : "Master parolni kiriting."
            };
            Controls.Add(_lblInfo);

            int y = 60;

            if (_firstRun)
            {
                Controls.Add(new Label { Left = 15, Top = y, Width = 515, Text = "PRIVATE key XML (<RSAKeyValue>...</RSAKeyValue>):" });
                y += 22;
                _txtPrivateKey = new TextBox
                {
                    Left = 15, Top = y, Width = 515, Height = 120,
                    Multiline = true, ScrollBars = ScrollBars.Vertical,
                    Font = new Font("Consolas", 8.5f)
                };
                Controls.Add(_txtPrivateKey);
                y += 130;
            }

            Controls.Add(new Label { Left = 15, Top = y, Width = 180, Text = "Master parol:" });
            _txtPassword = new TextBox { Left = 200, Top = y - 3, Width = 330, UseSystemPasswordChar = true };
            Controls.Add(_txtPassword);
            y += 32;

            if (_firstRun)
            {
                Controls.Add(new Label { Left = 15, Top = y, Width = 180, Text = "Master parol (takror):" });
                _txtPassword2 = new TextBox { Left = 200, Top = y - 3, Width = 330, UseSystemPasswordChar = true };
                Controls.Add(_txtPassword2);
                y += 32;
            }

            _btnOk = new Button { Left = 330, Top = y + 8, Width = 100, Height = 30, Text = _firstRun ? "Saqlash" : "Kirish" };
            _btnOk.Click += BtnOk_Click;
            Controls.Add(_btnOk);

            var btnCancel = new Button { Left = 440, Top = y + 8, Width = 90, Height = 30, Text = "Bekor", DialogResult = DialogResult.Cancel };
            Controls.Add(btnCancel);

            AcceptButton = _btnOk;
            CancelButton = btnCancel;
        }

        private void BtnOk_Click(object sender, EventArgs e)
        {
            string pwd = _txtPassword.Text;
            if (string.IsNullOrEmpty(pwd))
            {
                MessageBox.Show("Master parolni kiriting.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (_firstRun)
                {
                    string privXml = (_txtPrivateKey.Text ?? string.Empty).Trim();
                    if (!privXml.Contains("<RSAKeyValue>") || !privXml.Contains("<D>"))
                    {
                        MessageBox.Show("To'liq PRIVATE key XML kiriting (<D> qismi bo'lgan). Public key emas!",
                            "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    if (pwd != _txtPassword2.Text)
                    {
                        MessageBox.Show("Parollar mos kelmadi.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    PrivateKeyStore.Save(privXml, pwd);
                    PrivateKeyXml = privXml;
                    DialogResult = DialogResult.OK;
                    Close();
                }
                else
                {
                    string privXml = PrivateKeyStore.Load(pwd);
                    if (privXml == null)
                    {
                        MessageBox.Show("Master parol noto'g'ri.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        _txtPassword.SelectAll();
                        _txtPassword.Focus();
                        return;
                    }
                    PrivateKeyXml = privXml;
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Xatolik: " + ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
