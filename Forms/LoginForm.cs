// ============================================================
// FILE: Forms/LoginForm.cs
// The first window that appears when the app starts.
// Nobody can access the main window without logging in.
// ============================================================

using LeaveTrackerPro.Services;
using LeaveTrackerPro.Helpers;

namespace LeaveTrackerPro.Forms
{
    public class LoginForm : Form
    {
        private readonly AuthService _auth;

        private Label lblAppName = null!, lblUsername = null!, lblPassword = null!, lblError = null!;
        private TextBox txtUsername = null!, txtPassword = null!;
        private Button btnLogin = null!;
        private CheckBox chkShowPassword = null!;
        private Panel pnlLogo = null!, pnlForm = null!;

        public LoginForm(AuthService auth)
        {
            _auth = auth;
            BuildUI();
        }

        private void BuildUI()
        {
            this.Text = "Leave Request Tracker — Login";
            this.Size = new Size(420, 520);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.White;
            this.Font = new Font("Segoe UI", 9.5F);

            // ---- Logo / header panel ----
            pnlLogo = new Panel
            {
                Dock = DockStyle.Top,
                Height = 140,
                BackColor = Color.FromArgb(30, 80, 160)
            };

            lblAppName = new Label
            {
                Text = "Leave Request\nTracker",
                Font = new Font("Segoe UI", 20F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(380, 90),
                Location = new Point(20, 28),
                TextAlign = ContentAlignment.MiddleCenter
            };
            pnlLogo.Controls.Add(lblAppName);

            // ---- Form panel ----
            pnlForm = new Panel
            {
                Location = new Point(30, 160),
                Size = new Size(350, 300)
            };

            lblUsername = new Label { Text = "Username", Location = new Point(0, 10), AutoSize = true };

            txtUsername = new TextBox
            {
                Location = new Point(0, 30),
                Size = new Size(350, 30),
                Font = new Font("Segoe UI", 11F),
                PlaceholderText = "Enter your username"
            };

            lblPassword = new Label { Text = "Password", Location = new Point(0, 72), AutoSize = true };

            txtPassword = new TextBox
            {
                Location = new Point(0, 92),
                Size = new Size(350, 30),
                Font = new Font("Segoe UI", 11F),
                UseSystemPasswordChar = true,
                PlaceholderText = "Enter your password"
            };

            chkShowPassword = new CheckBox
            {
                Text = "Show password",
                Location = new Point(0, 130),
                AutoSize = true,
                ForeColor = Color.FromArgb(100, 100, 100)
            };
            chkShowPassword.CheckedChanged += (s, e) =>
                txtPassword.UseSystemPasswordChar = !chkShowPassword.Checked;

            lblError = new Label
            {
                Text = "",
                Location = new Point(0, 160),
                Size = new Size(350, 40),
                ForeColor = Color.FromArgb(180, 30, 30),
                Font = new Font("Segoe UI", 9F),
                TextAlign = ContentAlignment.MiddleCenter,
                Visible = false
            };

            btnLogin = new Button
            {
                Text = "Sign In",
                Location = new Point(0, 208),
                Size = new Size(350, 42),
                BackColor = Color.FromArgb(30, 80, 160),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnLogin.FlatAppearance.BorderSize = 0;
            btnLogin.Click += BtnLogin_Click;

            // Allow Enter key to trigger login
            txtPassword.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) BtnLogin_Click(s, e); };
            txtUsername.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) txtPassword.Focus(); };

            pnlForm.Controls.AddRange(new Control[] {
                lblUsername, txtUsername, lblPassword, txtPassword,
                chkShowPassword, lblError, btnLogin
            });

            this.Controls.Add(pnlLogo);
            this.Controls.Add(pnlForm);

            // Default credentials hint
            var lblHint = new Label
            {
                Text = "Default admin: admin / Admin@123",
                Location = new Point(30, 472),
                AutoSize = true,
                ForeColor = Color.FromArgb(160, 160, 160),
                Font = new Font("Segoe UI", 8F)
            };
            this.Controls.Add(lblHint);

            txtUsername.Focus();
        }

        private void BtnLogin_Click(object? sender, EventArgs e)
        {
            lblError.Visible = false;
            btnLogin.Enabled = false;
            btnLogin.Text = "Signing in...";

            var (success, error) = _auth.Login(txtUsername.Text, txtPassword.Text);

            btnLogin.Enabled = true;
            btnLogin.Text = "Sign In";

            if (success)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                lblError.Text = error;
                lblError.Visible = true;
                txtPassword.Clear();
                txtPassword.Focus();
            }
        }
    }
}