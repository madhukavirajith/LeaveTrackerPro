// ============================================================
// FILE: Forms/SupportForms.cs
// Three small dialog windows:
//   1. ChangePasswordForm  — any user changes their own password
//   2. UserForm            — admin creates a new user account
//   3. EmailSettingsForm   — admin configures SMTP email
// ============================================================

using LeaveTrackerPro.Services;
using LeaveTrackerPro.Models;
using LeaveTrackerPro.Data;

namespace LeaveTrackerPro.Forms
{
    // ==========================================================
    // 1. CHANGE PASSWORD FORM
    // ==========================================================
    public class ChangePasswordForm : Form
    {
        private readonly AuthService _auth;
        private TextBox txtCurrent, txtNew, txtConfirm;
        private Label lblError;
        private Button btnSave, btnCancel;

        public ChangePasswordForm(AuthService auth)
        {
            _auth = auth;
            this.Text = "Change Password";
            this.Size = new Size(380, 340);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5F);
            BuildUI();
        }

        private void BuildUI()
        {
            var pnl = new Panel { Location = new Point(24, 16), Size = new Size(320, 260) };

            Label L(string t, int y) => new Label { Text = t, Location = new Point(0, y), AutoSize = true };
            TextBox T(int y, string ph) => new TextBox
            {
                Location = new Point(0, y),
                Size = new Size(320, 26),
                UseSystemPasswordChar = true,
                PlaceholderText = ph,
                Font = new Font("Segoe UI", 10F)
            };

            pnl.Controls.Add(L("Current password", 0));
            txtCurrent = T(20, "Enter current password");
            pnl.Controls.Add(txtCurrent);

            pnl.Controls.Add(L("New password", 58));
            txtNew = T(78, "Min 8 chars, upper, lower, digit, symbol");
            pnl.Controls.Add(txtNew);

            pnl.Controls.Add(L("Confirm new password", 116));
            txtConfirm = T(136, "Re-enter new password");
            pnl.Controls.Add(txtConfirm);

            lblError = new Label
            {
                Location = new Point(0, 172),
                Size = new Size(320, 36),
                ForeColor = Color.FromArgb(180, 30, 30),
                Font = new Font("Segoe UI", 8.5F),
                Visible = false
            };
            pnl.Controls.Add(lblError);

            btnSave = new Button
            {
                Text = "Change Password",
                Location = new Point(0, 214),
                Size = new Size(156, 34),
                BackColor = Color.FromArgb(30, 80, 160),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(164, 214),
                Size = new Size(80, 34),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => this.Close();

            pnl.Controls.AddRange(new Control[] { btnSave, btnCancel });
            this.Controls.Add(pnl);
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            lblError.Visible = false;

            if (txtNew.Text != txtConfirm.Text)
            {
                lblError.Text = "New passwords do not match.";
                lblError.Visible = true;
                return;
            }

            var (ok, err) = _auth.ChangePassword(
                Helpers.Session.CurrentUser!.Id,
                txtCurrent.Text, txtNew.Text);

            if (!ok)
            {
                lblError.Text = err;
                lblError.Visible = true;
            }
            else
            {
                MessageBox.Show("Password changed successfully.", "Success",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.Close();
            }
        }
    }

    // ==========================================================
    // 2. CREATE USER FORM (Admin only)
    // ==========================================================
    public class UserForm : Form
    {
        private readonly UserService _users;
        private TextBox txtFullName, txtEmail, txtUsername, txtPassword;
        private ComboBox cmbDept, cmbRole;
        private Label lblError;
        private Button btnCreate, btnCancel;

        public UserForm(UserService users)
        {
            _users = users;
            this.Text = "Create New User";
            this.Size = new Size(420, 440);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5F);
            BuildUI();
        }

        private void BuildUI()
        {
            var pnl = new Panel { Location = new Point(24, 16), Size = new Size(360, 380) };

            Label L(string t, int y) => new Label { Text = t, Location = new Point(0, y), AutoSize = true };
            TextBox T(int y, string ph, bool pw = false) => new TextBox
            {
                Location = new Point(0, y),
                Size = new Size(360, 26),
                PlaceholderText = ph,
                Font = new Font("Segoe UI", 10F),
                UseSystemPasswordChar = pw
            };

            pnl.Controls.Add(L("Full Name *", 0));
            txtFullName = T(20, "e.g. John Silva");
            pnl.Controls.Add(txtFullName);

            pnl.Controls.Add(L("Email *", 56));
            txtEmail = T(76, "e.g. john@company.com");
            pnl.Controls.Add(txtEmail);

            pnl.Controls.Add(L("Username *", 112));
            txtUsername = T(132, "e.g. jsilva");
            pnl.Controls.Add(txtUsername);

            pnl.Controls.Add(L("Password *", 168));
            txtPassword = T(188, "Min 8 chars, upper, lower, digit, symbol", pw: true);
            pnl.Controls.Add(txtPassword);

            pnl.Controls.Add(L("Department *", 224));
            cmbDept = new ComboBox { Location = new Point(0, 244), Size = new Size(170, 26), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDept.Items.AddRange(new string[] { "HR", "IT", "Finance", "Operations", "Marketing", "Sales", "Legal", "Other" });
            pnl.Controls.Add(cmbDept);

            pnl.Controls.Add(L("Role *", 188));
            cmbRole = new ComboBox { Location = new Point(188, 244), Size = new Size(172, 26), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRole.Items.AddRange(Enum.GetNames(typeof(UserRole)));
            cmbRole.SelectedIndex = 0;
            pnl.Controls.Add(cmbRole);

            lblError = new Label
            {
                Location = new Point(0, 280),
                Size = new Size(360, 36),
                ForeColor = Color.FromArgb(180, 30, 30),
                Font = new Font("Segoe UI", 8.5F),
                Visible = false
            };
            pnl.Controls.Add(lblError);

            btnCreate = new Button
            {
                Text = "Create User",
                Location = new Point(0, 322),
                Size = new Size(170, 34),
                BackColor = Color.FromArgb(30, 80, 160),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnCreate.FlatAppearance.BorderSize = 0;
            btnCreate.Click += BtnCreate_Click;

            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(180, 322),
                Size = new Size(80, 34),
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            btnCancel.Click += (s, e) => this.Close();

            pnl.Controls.AddRange(new Control[] { btnCreate, btnCancel });
            this.Controls.Add(pnl);
        }

        private void BtnCreate_Click(object? sender, EventArgs e)
        {
            lblError.Visible = false;
            if (cmbDept.SelectedIndex < 0) { lblError.Text = "Select a department."; lblError.Visible = true; return; }

            var role = Enum.Parse<UserRole>(cmbRole.SelectedItem!.ToString()!);
            var (ok, err) = _users.CreateUser(
                txtFullName.Text, txtEmail.Text, txtUsername.Text,
                txtPassword.Text, cmbDept.SelectedItem!.ToString()!, role);

            if (!ok) { lblError.Text = err; lblError.Visible = true; }
            else
            {
                MessageBox.Show("User created successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }
    }

    // ==========================================================
    // 3. EMAIL SETTINGS FORM
    // ==========================================================
    public class EmailSettingsForm : Form
    {
        private TextBox txtHost, txtPort, txtUser, txtPassword, txtFrom, txtCompany;
        private CheckBox chkEnabled;
        private Button btnSave, btnTest, btnCancel;
        private Label lblStatus;

        public EmailSettingsForm()
        {
            this.Text = "Email Settings (SMTP)";
            this.Size = new Size(440, 420);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5F);
            BuildUI();
            LoadSettings();
        }

        private void BuildUI()
        {
            var pnl = new Panel { Location = new Point(24, 12), Size = new Size(384, 360) };

            Label L(string t, int y) => new Label { Text = t, Location = new Point(0, y), AutoSize = true };
            TextBox T(int y, bool pw = false) => new TextBox
            {
                Location = new Point(0, y),
                Size = new Size(384, 26),
                Font = new Font("Segoe UI", 10F),
                UseSystemPasswordChar = pw
            };

            pnl.Controls.Add(L("SMTP Host (e.g. smtp.gmail.com)", 0));
            txtHost = T(18); pnl.Controls.Add(txtHost);

            pnl.Controls.Add(L("Port (usually 587)", 52));
            txtPort = T(70); txtPort.Size = new Size(100, 26); pnl.Controls.Add(txtPort);

            pnl.Controls.Add(L("SMTP Username (email address)", 104));
            txtUser = T(122); pnl.Controls.Add(txtUser);

            pnl.Controls.Add(L("SMTP Password", 156));
            txtPassword = T(174, pw: true); pnl.Controls.Add(txtPassword);

            pnl.Controls.Add(L("From Address", 208));
            txtFrom = T(226); pnl.Controls.Add(txtFrom);

            pnl.Controls.Add(L("Company Name", 260));
            txtCompany = T(278); pnl.Controls.Add(txtCompany);

            chkEnabled = new CheckBox { Text = "Enable email notifications", Location = new Point(0, 310), AutoSize = true };
            pnl.Controls.Add(chkEnabled);

            lblStatus = new Label { Location = new Point(0, 336), Size = new Size(384, 22), Font = new Font("Segoe UI", 8.5F, FontStyle.Italic) };
            pnl.Controls.Add(lblStatus);

            btnSave = new Button { Text = "Save", Location = new Point(0, 362), Size = new Size(100, 32), BackColor = Color.FromArgb(30, 80, 160), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnSave.FlatAppearance.BorderSize = 0;
            btnSave.Click += BtnSave_Click;

            btnTest = new Button { Text = "Send Test Email", Location = new Point(110, 362), Size = new Size(130, 32), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnTest.Click += BtnTest_Click;

            btnCancel = new Button { Text = "Close", Location = new Point(250, 362), Size = new Size(80, 32), FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand };
            btnCancel.Click += (s, e) => this.Close();

            pnl.Controls.AddRange(new Control[] { btnSave, btnTest, btnCancel });
            this.Controls.Add(pnl);
        }

        private void LoadSettings()
        {
            using var db = new AppDbContext();
            var s = db.AppSettings.ToDictionary(x => x.Key, x => x.Value);
            txtHost.Text = s.GetValueOrDefault("SmtpHost", "");
            txtPort.Text = s.GetValueOrDefault("SmtpPort", "587");
            txtUser.Text = s.GetValueOrDefault("SmtpUser", "");
            txtPassword.Text = s.GetValueOrDefault("SmtpPassword", "");
            txtFrom.Text = s.GetValueOrDefault("SmtpFrom", "");
            txtCompany.Text = s.GetValueOrDefault("CompanyName", "");
            chkEnabled.Checked = s.GetValueOrDefault("EmailEnabled", "false").ToLower() == "true";
        }

        private void BtnSave_Click(object? sender, EventArgs e)
        {
            using var db = new AppDbContext();
            void Set(string k, string v)
            {
                var row = db.AppSettings.Find(k);
                if (row != null) row.Value = v; else db.AppSettings.Add(new Models.AppSetting { Key = k, Value = v });
            }
            Set("SmtpHost", txtHost.Text.Trim());
            Set("SmtpPort", txtPort.Text.Trim());
            Set("SmtpUser", txtUser.Text.Trim());
            Set("SmtpPassword", txtPassword.Text);
            Set("SmtpFrom", txtFrom.Text.Trim());
            Set("CompanyName", txtCompany.Text.Trim());
            Set("EmailEnabled", chkEnabled.Checked ? "true" : "false");
            db.SaveChanges();
            lblStatus.ForeColor = Color.FromArgb(0, 120, 50);
            lblStatus.Text = "Settings saved successfully.";
        }

        private void BtnTest_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtUser.Text))
            {
                MessageBox.Show("Enter SMTP credentials first.", "Missing Info", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            lblStatus.ForeColor = Color.FromArgb(30, 80, 160);
            lblStatus.Text = "Sending test email...";
            Application.DoEvents();
            // A real test send would go here using EmailService
            lblStatus.Text = "Test feature: save settings and use real request flow to test.";
        }
    }
}