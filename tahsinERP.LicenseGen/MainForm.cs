using System;
using System.Drawing;
using System.Security.Cryptography;
using System.Windows.Forms;
using tahsinERP.Licensing;

namespace tahsinERP.LicenseGen
{
    public class MainForm : Form
    {
        private readonly string _privateKeyXml;
        private readonly string _publicKeyXml;
        private LicenseDb _db;

        // Ulanish (ixtiyoriy)
        private TextBox _txtConn;
        private Button _btnConnect;
        private Label _lblConnStatus;

        // Kirish maydonlari
        private ComboBox _cmbCompany;
        private Label _lblUserCount;
        private NumericUpDown _numCompanyId;
        private TextBox _txtCompanyName;
        private TextBox _txtInstallationId;
        private NumericUpDown _numMaxUsers;
        private DateTimePicker _dtStart;
        private DateTimePicker _dtExpiry;
        private ComboBox _cmbEdition;

        // Natija
        private TextBox _txtResult;
        private Button _btnGenerate;
        private Button _btnCopy;
        private Button _btnSaveDb;

        // Reestr
        private DataGridView _grid;

        // Tekshirish
        private TextBox _txtVerify;
        private Label _lblVerify;

        public MainForm(string privateKeyXml)
        {
            _privateKeyXml = privateKeyXml;
            _publicKeyXml = DerivePublicKey(privateKeyXml);
            BuildUi();
            WarnIfKeyMismatch();
            InitDb(silent: true); // mijoz bazasi ixtiyoriy — ulanmasa ham generator ishlaydi
            LoadHistory();
        }

        private static string DerivePublicKey(string privateXml)
        {
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(privateXml);
                return rsa.ToXmlString(false);
            }
        }

        /// <summary>
        /// Private key ERP ichidagi public key (LicenseHelper.PublicKeyXml) bilan juft
        /// bo'lmasa, chiqarilgan kalitlarni ERP qabul qilmaydi — ogohlantiramiz.
        /// </summary>
        private void WarnIfKeyMismatch()
        {
            if (!string.Equals(Normalize(_publicKeyXml), Normalize(LicenseHelper.PublicKeyXml), StringComparison.Ordinal))
            {
                MessageBox.Show(
                    "Diqqat: ochilgan private key ERP ichidagi public key (LicenseHelper.PublicKeyXml) bilan juft emas.\n" +
                    "Bu generator chiqargan kalitlarni ERP QABUL QILMAYDI.",
                    "Kalit jufti mos emas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private static string Normalize(string xml)
        {
            return (xml ?? string.Empty).Replace(" ", "").Replace("\r", "").Replace("\n", "");
        }

        private void BuildUi()
        {
            Text = "TAHSIN ERP — Litsenziya generatori";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 900;
            Height = 800;
            Font = new Font("Segoe UI", 9f);
            MinimumSize = new Size(880, 760);

            // ---- Ulanish paneli (ixtiyoriy) ----
            var grpConn = new GroupBox { Left = 12, Top = 8, Width = 860, Height = 70, Text = "Mijoz bazasiga ulanish (ixtiyoriy — kompaniyalar ro'yxati va to'g'ridan-to'g'ri yozish uchun)", Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _txtConn = new TextBox { Left = 12, Top = 28, Width = 620, Text = LicenseDb.DefaultConnectionString(), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _btnConnect = new Button { Left = 640, Top = 26, Width = 100, Height = 25, Text = "Ulanish", Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnConnect.Click += (s, e) => InitDb(silent: false);
            _lblConnStatus = new Label { Left = 748, Top = 30, Width = 100, Text = "", Anchor = AnchorStyles.Top | AnchorStyles.Right };
            grpConn.Controls.AddRange(new Control[] { _txtConn, _btnConnect, _lblConnStatus });
            Controls.Add(grpConn);

            // ---- Kalit parametrlari ----
            var grp = new GroupBox { Left = 12, Top = 84, Width = 860, Height = 250, Text = "Yangi litsenziya (v2 — o'rnatmaga bog'langan)", Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            grp.Controls.Add(new Label { Left = 12, Top = 30, Width = 120, Text = "Bazadan tanlash:" });
            _cmbCompany = new ComboBox { Left = 140, Top = 27, Width = 400, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbCompany.SelectedIndexChanged += Company_Changed;
            grp.Controls.Add(_cmbCompany);
            _lblUserCount = new Label { Left = 552, Top = 30, Width = 290, Text = "", ForeColor = Color.DimGray };
            grp.Controls.Add(_lblUserCount);

            grp.Controls.Add(new Label { Left = 12, Top = 64, Width = 120, Text = "CompanyID:" });
            _numCompanyId = new NumericUpDown { Left = 140, Top = 61, Width = 100, Minimum = 1, Maximum = int.MaxValue, Value = 1 };
            grp.Controls.Add(_numCompanyId);
            grp.Controls.Add(new Label { Left = 260, Top = 64, Width = 85, Text = "Mijoz nomi:" });
            _txtCompanyName = new TextBox { Left = 350, Top = 61, Width = 300 };
            grp.Controls.Add(_txtCompanyName);

            grp.Controls.Add(new Label { Left = 12, Top = 98, Width = 120, Text = "O'rnatma ID:" });
            _txtInstallationId = new TextBox { Left = 140, Top = 95, Width = 320, Font = new Font("Consolas", 9f) };
            grp.Controls.Add(_txtInstallationId);
            grp.Controls.Add(new Label { Left = 470, Top = 98, Width = 380, Text = "(mijozning ERP → Litsenziya sahifasidan nusxa oling)", ForeColor = Color.DimGray });

            grp.Controls.Add(new Label { Left = 12, Top = 132, Width = 120, Text = "Max foydalanuvchi:" });
            _numMaxUsers = new NumericUpDown { Left = 140, Top = 129, Width = 100, Minimum = 0, Maximum = 100000, Value = 10 };
            grp.Controls.Add(_numMaxUsers);
            grp.Controls.Add(new Label { Left = 246, Top = 132, Width = 200, Text = "(0 = cheksiz)", ForeColor = Color.DimGray });

            grp.Controls.Add(new Label { Left = 12, Top = 166, Width = 120, Text = "Boshlanish:" });
            _dtStart = new DateTimePicker { Left = 140, Top = 163, Width = 160, Format = DateTimePickerFormat.Short, Value = DateTime.Today };
            grp.Controls.Add(_dtStart);

            grp.Controls.Add(new Label { Left = 320, Top = 166, Width = 90, Text = "Tugash:" });
            _dtExpiry = new DateTimePicker { Left = 410, Top = 163, Width = 160, Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddYears(1) };
            grp.Controls.Add(_dtExpiry);

            grp.Controls.Add(new Label { Left = 12, Top = 202, Width = 120, Text = "Tarif:" });
            _cmbEdition = new ComboBox { Left = 140, Top = 199, Width = 160, DropDownStyle = ComboBoxStyle.DropDown };
            _cmbEdition.Items.AddRange(new object[] { "Basic", "Pro", "Enterprise" });
            _cmbEdition.SelectedIndex = 1;
            grp.Controls.Add(_cmbEdition);

            _btnGenerate = new Button { Left = 620, Top = 192, Width = 220, Height = 40, Text = "🔑  KALIT YARATISH", Anchor = AnchorStyles.Top | AnchorStyles.Right, BackColor = Color.FromArgb(46, 125, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
            _btnGenerate.Click += Generate_Click;
            grp.Controls.Add(_btnGenerate);

            Controls.Add(grp);

            // ---- Natija ----
            var grpRes = new GroupBox { Left = 12, Top = 342, Width = 860, Height = 110, Text = "Natija (kalit)", Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            _txtResult = new TextBox { Left = 12, Top = 26, Width = 836, Height = 44, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Font = new Font("Consolas", 9f), BackColor = Color.White, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            grpRes.Controls.Add(_txtResult);
            _btnCopy = new Button { Left = 640, Top = 74, Width = 100, Height = 26, Text = "Nusxa olish", Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnCopy.Click += (s, e) => { if (_txtResult.TextLength > 0) Clipboard.SetText(_txtResult.Text); };
            _btnSaveDb = new Button { Left = 748, Top = 74, Width = 100, Height = 26, Text = "Mijoz bazasiga", Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            _btnSaveDb.Click += SaveDb_Click;
            grpRes.Controls.AddRange(new Control[] { _btnCopy, _btnSaveDb });
            Controls.Add(grpRes);

            // ---- Reestr ----
            var grpHist = new GroupBox { Left = 12, Top = 458, Width = 860, Height = 190, Text = "Chiqarilgan kalitlar reestri (lokal: " + IssuedLicenseRegistry.FilePath + ")", Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            _grid = new DataGridView { Left = 12, Top = 22, Width = 836, Height = 158, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            grpHist.Controls.Add(_grid);
            Controls.Add(grpHist);

            // ---- Tekshirish ----
            var grpVer = new GroupBox { Left = 12, Top = 654, Width = 860, Height = 84, Text = "Kalitni tekshirish", Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            _txtVerify = new TextBox { Left = 12, Top = 26, Width = 620, Font = new Font("Consolas", 9f), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            var btnVerify = new Button { Left = 640, Top = 24, Width = 100, Height = 25, Text = "Tekshirish", Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            btnVerify.Click += Verify_Click;
            _lblVerify = new Label { Left = 12, Top = 54, Width = 836, Text = "", Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            grpVer.Controls.AddRange(new Control[] { _txtVerify, btnVerify, _lblVerify });
            Controls.Add(grpVer);
        }

        private void InitDb(bool silent)
        {
            try
            {
                _db = new LicenseDb(_txtConn.Text.Trim());
                _db.TestConnection();
                _lblConnStatus.Text = "✅ ulandi";
                _lblConnStatus.ForeColor = Color.Green;
                LoadCompanies();
            }
            catch (Exception ex)
            {
                _db = null;
                _lblConnStatus.Text = silent ? "ulanmagan" : "❌ xato";
                _lblConnStatus.ForeColor = silent ? Color.DimGray : Color.Red;
                if (!silent)
                    MessageBox.Show("Bazaga ulanib bo'lmadi:\n" + ex.Message, "Ulanish xatosi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            _btnSaveDb.Enabled = _db != null && _txtResult.TextLength > 0;
        }

        private void LoadCompanies()
        {
            _cmbCompany.Items.Clear();
            foreach (var c in _db.GetCompanies())
                _cmbCompany.Items.Add(c);
            if (_cmbCompany.Items.Count > 0)
                _cmbCompany.SelectedIndex = 0;
        }

        private void Company_Changed(object sender, EventArgs e)
        {
            var c = _cmbCompany.SelectedItem as CompanyRow;
            if (c == null || _db == null) return;

            _numCompanyId.Value = Math.Max(1, c.ID);
            _txtCompanyName.Text = c.Name;

            try
            {
                int userCount = _db.GetActiveUserCount(c.ID);
                _lblUserCount.Text = "Faol xodimlar: " + userCount + " (taklif)";
                // Auto-suggest: hozirgi xodim soni yoki kamida 5
                _numMaxUsers.Value = Math.Max(userCount, 5);
            }
            catch (Exception ex)
            {
                _lblUserCount.Text = "xato: " + ex.Message;
            }
        }

        private void LoadHistory()
        {
            var table = new System.Data.DataTable();
            table.Columns.Add("Chiqarilgan", typeof(string));
            table.Columns.Add("Kim", typeof(string));
            table.Columns.Add("CompanyID", typeof(int));
            table.Columns.Add("Mijoz", typeof(string));
            table.Columns.Add("O'rnatma ID", typeof(string));
            table.Columns.Add("Max", typeof(int));
            table.Columns.Add("Boshlanish", typeof(string));
            table.Columns.Add("Tugash", typeof(string));
            table.Columns.Add("Tarif", typeof(string));
            table.Columns.Add("Ver", typeof(int));
            table.Columns.Add("Barmoq izi", typeof(string));

            try
            {
                var rows = IssuedLicenseRegistry.ReadAll();
                rows.Reverse(); // yangilari tepada
                foreach (var r in rows)
                {
                    table.Rows.Add(r.IssuedAt.ToString("dd.MM.yyyy HH:mm"), r.IssuedBy, r.CompanyID, r.CompanyName,
                        r.InstallationId, r.MaxUsers, r.StartDate.ToString("dd.MM.yyyy"), r.ExpiryDate.ToString("dd.MM.yyyy"),
                        r.Edition, r.KeyVersion, r.Fingerprint);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Reestrni o'qishda xato: " + ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            _grid.DataSource = null;
            _grid.DataSource = table;
        }

        private void Generate_Click(object sender, EventArgs e)
        {
            string installationId = LicenseHelper.NormalizeInstallationId(_txtInstallationId.Text);
            if (installationId == null)
            {
                MessageBox.Show("O'rnatma ID kiritilmagan yoki noto'g'ri (GUID bo'lishi kerak).\n" +
                                "Mijozning ERP → Litsenziya sahifasidan nusxa oling.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (_dtExpiry.Value.Date <= _dtStart.Value.Date)
            {
                MessageBox.Show("Tugash sanasi boshlanishdan keyin bo'lishi kerak.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int companyId = (int)_numCompanyId.Value;
            try
            {
                string key = LicenseHelper.GenerateKey(
                    companyId, (int)_numMaxUsers.Value, _dtStart.Value.Date, _dtExpiry.Value.Date,
                    _cmbEdition.Text, installationId, _privateKeyXml);

                // Har bir chiqarilgan kalit vendor reestriga yoziladi (yozilmasa — kalit ko'rsatilmaydi)
                IssuedLicenseRegistry.Append(new IssuedLicenseRow
                {
                    IssuedAt = DateTime.Now,
                    IssuedBy = Environment.UserDomainName + "\\" + Environment.UserName,
                    CompanyID = companyId,
                    CompanyName = _txtCompanyName.Text.Trim(),
                    InstallationId = installationId,
                    MaxUsers = (int)_numMaxUsers.Value,
                    StartDate = _dtStart.Value.Date,
                    ExpiryDate = _dtExpiry.Value.Date,
                    Edition = _cmbEdition.Text,
                    KeyVersion = 2,
                    Fingerprint = IssuedLicenseRegistry.Fingerprint(key),
                    LicenseKey = key
                });

                _txtInstallationId.Text = installationId;
                _txtResult.Text = key;
                _btnCopy.Enabled = true;
                _btnSaveDb.Enabled = _db != null;
                LoadHistory();
            }
            catch (Exception ex)
            {
                _txtResult.Text = "";
                _btnCopy.Enabled = false;
                _btnSaveDb.Enabled = false;
                MessageBox.Show("Kalit yaratishda xato: " + ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveDb_Click(object sender, EventArgs e)
        {
            if (_txtResult.TextLength == 0 || _db == null) return;

            LicenseHelper.LicenseInfo info;
            string key = _txtResult.Text.Trim();
            if (!LicenseHelper.TryParseKey(key, _publicKeyXml, out info) || info == null)
            {
                MessageBox.Show("Kalit yaroqsiz.", "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (MessageBox.Show(
                    "Kalit MIJOZ bazasiga to'g'ridan-to'g'ri yoziladi va CompanyID=" + info.CompanyID +
                    " ning eski litsenziyalari faolsizlantiriladi.\n" +
                    "Odatda kalitni mijoz o'zi ERP → Litsenziya sahifasida faollashtiradi. Davom etamizmi?",
                    "Tasdiqlash", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            try
            {
                int id = _db.InsertLicense(info.CompanyID, key, info.MaxUsers,
                    info.StartDate, info.ExpiryDate, info.Edition, "LicenseGen");
                MessageBox.Show("Saqlandi. Litsenziya ID: " + id + "\nERP yangi kalitni 5 daqiqa ichida ko'radi.",
                    "Muvaffaqiyat", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Bazaga yozishda xato: " + ex.Message, "Xato", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void Verify_Click(object sender, EventArgs e)
        {
            LicenseHelper.LicenseInfo info;
            bool ok = LicenseHelper.TryParseKey(_txtVerify.Text.Trim(), _publicKeyXml, out info);
            if (!ok || info == null)
            {
                _lblVerify.ForeColor = Color.Red;
                _lblVerify.Text = "❌ Yaroqsiz yoki soxta kalit (imzo mos kelmadi).";
                return;
            }
            _lblVerify.ForeColor = info.IsValid ? Color.Green : Color.DarkOrange;
            _lblVerify.Text = string.Format(
                "{0}  v{1}, CompanyID={2}, Max={3}, {4:dd.MM.yyyy} — {5:dd.MM.yyyy}, Tarif={6}, O'rnatma={7}, {8}",
                info.IsValid ? "✅ Yaroqli." : "⚠ Imzo to'g'ri, lekin muddati o'tgan/boshlanmagan.",
                info.Version, info.CompanyID, info.MaxUsers, info.StartDate, info.ExpiryDate, info.Edition,
                info.InstallationId ?? "(bog'lanmagan, v1)",
                info.IsExpired ? "muddati tugagan" : (info.IsNotStarted ? "hali boshlanmagan" : info.DaysRemaining + " kun qoldi"));
        }
    }
}
