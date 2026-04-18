// ============================================================
// FILE: Forms/MainForm.cs
// The main window. Layout changes based on role:
//   Employee  — sees only their own requests + balance panel
//   Manager   — sees their department + approve/reject actions
//   Admin     — sees everything + User Management tab
// ============================================================

using LeaveTrackerPro.Data;
using LeaveTrackerPro.Helpers;
using LeaveTrackerPro.Models;
using LeaveTrackerPro.Services;

namespace LeaveTrackerPro.Forms
{
    public class MainForm : Form
    {
        private readonly LeaveService _leave;
        private readonly UserService _users;
        private readonly AuthService _auth;
        private readonly ExportService _export;
        private readonly AuditService _audit;

        // Controls
        private Panel pnlHeader, pnlStats;
        private TabControl tabMain;
        private TabPage tabRequests, tabBalance, tabAudit, tabUsers, tabSettings;
        private DataGridView dgvRequests, dgvBalance, dgvAudit, dgvUsers;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel lblStatus;

        // Filter bar
        private TextBox txtSearch;
        private ComboBox cmbStatus, cmbDept, cmbYear;
        private Button btnSearch, btnShowAll, btnExport;

        // Submit panel
        private GroupBox grpSubmit;
        private TextBox txtReason;
        private ComboBox cmbLeaveType;
        private DateTimePicker dtpStart, dtpEnd;
        private Button btnSubmit, btnClear;
        private Label lblDaysCalc;

        // Manager panel
        private GroupBox grpManager;
        private TextBox txtMgrNote;
        private Button btnApprove, btnReject, btnCancel;

        // Stats labels
        private Label lblTotal, lblPending, lblApproved, lblRejected, lblWelcome;

        private int _selectedRequestId = -1;

        public MainForm(LeaveService leave, UserService users,
                        AuthService auth, ExportService export, AuditService audit)
        {
            _leave = leave;
            _users = users;
            _auth = auth;
            _export = export;
            _audit = audit;
            BuildUI();
            LoadData();
        }

        // ============================================================
        // BUILD UI
        // ============================================================
        private void BuildUI()
        {
            this.Text = "Leave Request Tracker Pro";
            this.Size = new Size(1280, 800);
            this.MinimumSize = new Size(1050, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F);
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.DoubleBuffered = true;

            BuildHeader();
            BuildStats();
            BuildMainTabs();
            BuildStatusBar();

            this.PerformLayout();
        }

        private void BuildHeader()
        {
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 56,
                BackColor = Color.FromArgb(30, 80, 160),
                Padding = new Padding(16, 0, 16, 0)
            };

            lblWelcome = new Label
            {
                Text = $"Leave Request Tracker Pro   |   {Session.CurrentUser!.FullName}  [{Session.CurrentUser.Role}]  —  {Session.CurrentUser.Department}",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = false,
                Size = new Size(900, 56),
                Location = new Point(16, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var btnLogout = new Button
            {
                Text = "Logout",
                Size = new Size(80, 30),
                Location = new Point(1170, 13),
                BackColor = Color.FromArgb(255, 80, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            btnLogout.FlatAppearance.BorderSize = 0;
            btnLogout.Click += (s, e) =>
            {
                _auth.Logout();
                Application.Restart();
            };

            var btnChangePass = new Button
            {
                Text = "Change Password",
                Size = new Size(130, 30),
                Location = new Point(1028, 13),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 100, 190),
                Cursor = Cursors.Hand
            };
            btnChangePass.FlatAppearance.BorderSize = 0;
            btnChangePass.FlatAppearance.BorderColor = Color.FromArgb(80, 140, 220);
            btnChangePass.Click += (s, e) =>
            {
                using var f = new ChangePasswordForm(_auth);
                f.ShowDialog(this);
            };

            pnlHeader.Controls.AddRange(new Control[] { lblWelcome, btnChangePass, btnLogout });
            this.Controls.Add(pnlHeader);
        }

        private void BuildStats()
        {
            pnlStats = new Panel
            {
                Dock = DockStyle.Top,
                Height = 64,
                BackColor = Color.White,
                Padding = new Padding(12, 8, 12, 8)
            };
            pnlStats.Paint += (s, e) =>
                e.Graphics.DrawLine(new Pen(Color.FromArgb(220, 225, 235)),
                    0, pnlStats.Height - 1, pnlStats.Width, pnlStats.Height - 1);

            void MakeStat(ref Label lbl, string text, Color fg, int x)
            {
                lbl = new Label
                {
                    Text = text,
                    Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                    ForeColor = fg,
                    AutoSize = false,
                    Size = new Size(170, 44),
                    Location = new Point(x, 10),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BackColor = Color.FromArgb(248, 250, 255),
                    BorderStyle = BorderStyle.FixedSingle
                };
                pnlStats.Controls.Add(lbl);
            }

            MakeStat(ref lblTotal, "Total: —", Color.FromArgb(50, 50, 50), 10);
            MakeStat(ref lblPending, "Pending: —", Color.FromArgb(160, 100, 0), 192);
            MakeStat(ref lblApproved, "Approved: —", Color.FromArgb(0, 120, 50), 374);
            MakeStat(ref lblRejected, "Rejected: —", Color.FromArgb(170, 20, 20), 556);

            this.Controls.Add(pnlStats);
        }

        private void BuildMainTabs()
        {
            // Create with minimal options first
            tabMain = new TabControl();
            tabMain.Dock = DockStyle.Fill;
            tabMain.Font = new Font("Segoe UI", 9.5F);
            tabMain.Padding = new Point(6, 3);

            tabRequests = new TabPage("Leave Requests");
            tabBalance = new TabPage("My Balance");
            tabAudit = new TabPage("Audit Log");
            tabUsers = new TabPage("User Management");
            tabSettings = new TabPage("Settings");

            BuildRequestsTab();
            BuildBalanceTab();
            BuildAuditTab();

            tabMain.TabPages.Add(tabRequests);
            tabMain.TabPages.Add(tabBalance);
            tabMain.TabPages.Add(tabAudit);

            if (Session.IsAdmin)
            {
                try
                {
                    BuildUsersTab();
                    tabMain.TabPages.Add(tabUsers);
                }
                catch { }

                try
                {
                    BuildSettingsTab();
                    tabMain.TabPages.Add(tabSettings);
                }
                catch { }
            }

            tabMain.SelectedIndexChanged += (s, e) => LoadCurrentTab();
            tabMain.SelectedIndex = 0;

            this.Controls.Add(tabMain);
            tabMain.BringToFront();
        }

        private void BuildRequestsTab()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

            // ---- Left: submit + manager panels ----
            var pnlLeft = new Panel { Dock = DockStyle.Left, Width = 310, Padding = new Padding(4) };

            grpSubmit = new GroupBox
            {
                Text = "Submit Leave Request",
                Location = new Point(4, 4),
                Size = new Size(298, 340),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.White
            };

            var lblType = MakeLabel("Leave Type *", 10, 24);
            cmbLeaveType = new ComboBox { Location = new Point(10, 42), Size = new Size(274, 24), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbLeaveType.Items.AddRange(LeaveService.LeaveEntitlements.Keys.ToArray<object>());
            cmbLeaveType.SelectedIndexChanged += RecalcDays;

            var lblStart = MakeLabel("Start Date *", 10, 70);
            dtpStart = new DateTimePicker { Location = new Point(10, 88), Size = new Size(130, 24), Format = DateTimePickerFormat.Short, MinDate = DateTime.Today };
            dtpStart.ValueChanged += RecalcDays;

            var lblEnd = MakeLabel("End Date *", 148, 70);
            dtpEnd = new DateTimePicker { Location = new Point(148, 88), Size = new Size(136, 24), Format = DateTimePickerFormat.Short, MinDate = DateTime.Today };
            dtpEnd.ValueChanged += RecalcDays;

            lblDaysCalc = new Label { Location = new Point(10, 118), Size = new Size(274, 20), Font = new Font("Segoe UI", 8.5F, FontStyle.Italic), ForeColor = Color.FromArgb(30, 80, 160) };

            var lblReason = MakeLabel("Reason", 10, 144);
            txtReason = new TextBox { Location = new Point(10, 162), Size = new Size(274, 60), Multiline = true, ScrollBars = ScrollBars.Vertical, PlaceholderText = "Optional reason" };

            btnSubmit = MakeButton("Submit Request", 10, 238, Color.FromArgb(30, 80, 160));
            btnClear = MakeButton("Clear", 168, 238, Color.FromArgb(200, 200, 200), Color.Black);
            btnSubmit.Click += BtnSubmit_Click;
            btnClear.Click += (s, e) => ClearSubmitForm();

            grpSubmit.Controls.AddRange(new Control[] {
                lblType, cmbLeaveType, lblStart, dtpStart, lblEnd, dtpEnd,
                lblDaysCalc, lblReason, txtReason, btnSubmit, btnClear
            });

            // Hide submit form for managers/admins (they don't need it on same screen)
            // They can still submit from a separate button if needed
            grpSubmit.Visible = !Session.IsManager;

            // ---- Manager actions ----
            grpManager = new GroupBox
            {
                Text = "Manager Actions",
                Location = new Point(4, Session.IsManager ? 4 : 352),
                Size = new Size(298, 170),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.White,
                Visible = Session.IsManager
            };

            var lblNote = MakeLabel("Decision note:", 10, 22);
            txtMgrNote = new TextBox { Location = new Point(10, 40), Size = new Size(274, 52), Multiline = true, PlaceholderText = "Required when rejecting" };
            btnApprove = MakeButton("✔ Approve", 10, 104, Color.FromArgb(0, 130, 60));
            btnReject = MakeButton("✘ Reject", 108, 104, Color.FromArgb(180, 30, 30));
            btnCancel = MakeButton("Cancel", 206, 104, Color.FromArgb(100, 100, 100));
            btnApprove.Size = btnReject.Size = btnCancel.Size = new Size(90, 34);
            btnApprove.Click += BtnApprove_Click;
            btnReject.Click += BtnReject_Click;
            btnCancel.Click += BtnCancel_Click;
            grpManager.Controls.AddRange(new Control[] { lblNote, txtMgrNote, btnApprove, btnReject, btnCancel });

            // For employees — allow submit + show manager panel is hidden
            if (!Session.IsManager)
            {
                var grpEmpManager = new GroupBox
                {
                    Text = "Manager Actions",
                    Location = new Point(4, 352),
                    Size = new Size(298, 130),
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                    BackColor = Color.White
                };
                var noteLbl = MakeLabel("Select a row to cancel a pending request.", 10, 24);
                noteLbl.Size = new Size(274, 40);
                var btnEmpCancel = MakeButton("Cancel Request", 10, 70, Color.FromArgb(100, 100, 100));
                btnEmpCancel.Click += BtnCancel_Click;
                grpEmpManager.Controls.AddRange(new Control[] { noteLbl, btnEmpCancel });
                pnlLeft.Controls.Add(grpEmpManager);
            }

            pnlLeft.Controls.AddRange(new Control[] { grpSubmit, grpManager });

            // ---- Right: filter + grid ----
            var pnlRight = new Panel { Dock = DockStyle.Fill, Padding = new Padding(4) };

            var grpFilter = new GroupBox
            {
                Text = "Search & Filter",
                Location = new Point(4, 4),
                Size = new Size(920, 60),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

            var lblSrch = MakeLabel("Name:", 8, 24);
            txtSearch = new TextBox { Location = new Point(52, 22), Size = new Size(160, 23), PlaceholderText = "Search name" };
            txtSearch.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) LoadRequestsGrid(); };

            var lblStat = MakeLabel("Status:", 222, 24);
            cmbStatus = new ComboBox { Location = new Point(268, 22), Size = new Size(110, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbStatus.Items.AddRange(new string[] { "All", "Pending", "Approved", "Rejected", "Cancelled" });
            cmbStatus.SelectedIndex = 0;

            var lblDeptF = MakeLabel("Dept:", 390, 24);
            cmbDept = new ComboBox { Location = new Point(430, 22), Size = new Size(110, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDept.Items.Add("All Departments");
            foreach (var u in _users.GetAll()) if (!cmbDept.Items.Contains(u.Department)) cmbDept.Items.Add(u.Department);
            cmbDept.SelectedIndex = 0;

            var lblYearF = MakeLabel("Year:", 552, 24);
            cmbYear = new ComboBox { Location = new Point(592, 22), Size = new Size(80, 23), DropDownStyle = ComboBoxStyle.DropDownList };
            for (int y = DateTime.Today.Year; y >= DateTime.Today.Year - 4; y--) cmbYear.Items.Add(y);
            cmbYear.Items.Insert(0, "All Years");
            cmbYear.SelectedIndex = 0;

            btnSearch = MakeButton("Search", 684, 19, Color.FromArgb(30, 80, 160), buttonHeight: 26);
            btnShowAll = MakeButton("Reset", 760, 19, Color.FromArgb(100, 100, 100), buttonHeight: 26);
            btnExport = MakeButton("Export Excel", 830, 19, Color.FromArgb(0, 130, 60), buttonHeight: 26);
            btnSearch.Size = btnShowAll.Size = new Size(68, 26);
            btnExport.Size = new Size(100, 26);

            btnSearch.Click += (s, e) => LoadRequestsGrid();
            btnShowAll.Click += (s, e) => { txtSearch.Clear(); cmbStatus.SelectedIndex = 0; cmbDept.SelectedIndex = 0; cmbYear.SelectedIndex = 0; LoadRequestsGrid(); };
            btnExport.Click += BtnExport_Click;

            grpFilter.Controls.AddRange(new Control[] {
                lblSrch, txtSearch, lblStat, cmbStatus, lblDeptF, cmbDept,
                lblYearF, cmbYear, btnSearch, btnShowAll, btnExport
            });

            // ---- Grid ----
            dgvRequests = BuildGrid();
            dgvRequests.Location = new Point(4, 72);
            dgvRequests.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dgvRequests.Size = new Size(920, 580);
            dgvRequests.SelectionChanged += DgvRequests_SelectionChanged;
            dgvRequests.RowPrePaint += ColorByStatus;

            SetupRequestColumns();

            pnlRight.Controls.AddRange(new Control[] { grpFilter, dgvRequests });
            pnl.Controls.Add(pnlRight);
            pnl.Controls.Add(pnlLeft);
            tabRequests.Controls.Add(pnl);
        }

        private void SetupRequestColumns()
        {
            dgvRequests.Columns.Clear();
            void AddCol(string name, string header, int fw = 100)
            {
                dgvRequests.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = name,
                    HeaderText = header,
                    DataPropertyName = name,
                    FillWeight = fw,
                    SortMode = DataGridViewColumnSortMode.Automatic
                });
            }
            AddCol("Id", "ID", 30);
            AddCol("EmployeeName", "Employee", 120);
            AddCol("Department", "Dept", 80);
            AddCol("LeaveType", "Type", 110);
            AddCol("StartDate", "Start", 70);
            AddCol("EndDate", "End", 70);
            AddCol("TotalDays", "Days", 40);
            AddCol("StatusStr", "Status", 70);
            AddCol("SubmittedAt", "Submitted", 90);
            AddCol("ReviewedByName", "Reviewed By", 90);
            AddCol("ManagerNote", "Note", 120);

            dgvRequests.Columns["StartDate"].DefaultCellStyle.Format = "dd/MM/yyyy";
            dgvRequests.Columns["EndDate"].DefaultCellStyle.Format = "dd/MM/yyyy";
            dgvRequests.Columns["SubmittedAt"].DefaultCellStyle.Format = "dd/MM/yyyy HH:mm";
            dgvRequests.Columns["Id"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvRequests.Columns["TotalDays"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvRequests.Columns["StatusStr"].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        }

        private void BuildBalanceTab()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

            var lbl = new Label
            {
                Text = $"Leave Balance for {Session.CurrentUser!.FullName} — {DateTime.Today.Year}",
                Font = new Font("Segoe UI", 12F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 36,
                ForeColor = Color.FromArgb(30, 80, 160)
            };

            dgvBalance = BuildGrid();
            dgvBalance.Dock = DockStyle.Fill;

            dgvBalance.Columns.Add(new DataGridViewTextBoxColumn { Name = "LeaveType", HeaderText = "Leave Type", FillWeight = 150 });
            dgvBalance.Columns.Add(new DataGridViewTextBoxColumn { Name = "TotalEntitlement", HeaderText = "Entitled Days", FillWeight = 80 });
            dgvBalance.Columns.Add(new DataGridViewTextBoxColumn { Name = "DaysUsed", HeaderText = "Used", FillWeight = 80 });
            dgvBalance.Columns.Add(new DataGridViewTextBoxColumn { Name = "DaysPending", HeaderText = "Pending", FillWeight = 80 });
            dgvBalance.Columns.Add(new DataGridViewTextBoxColumn { Name = "DaysRemaining", HeaderText = "Remaining", FillWeight = 80 });

            foreach (DataGridViewColumn col in dgvBalance.Columns)
                if (col.Name != "LeaveType")
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            pnl.Controls.Add(dgvBalance);
            pnl.Controls.Add(lbl);
            tabBalance.Controls.Add(pnl);
        }

        private void BuildAuditTab()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

            var lbl = new Label
            {
                Text = "Audit Log — all actions are recorded here",
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 32,
                ForeColor = Color.FromArgb(30, 80, 160)
            };

            dgvAudit = BuildGrid();
            dgvAudit.Dock = DockStyle.Fill;
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Time", DataPropertyName = "TimestampLocal", FillWeight = 90 });
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "User", DataPropertyName = "ActorName", FillWeight = 100 });
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Action", DataPropertyName = "Action", FillWeight = 80 });
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Request #", DataPropertyName = "LeaveRequestId", FillWeight = 60 });
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Details", DataPropertyName = "Details", FillWeight = 200 });
            dgvAudit.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Machine", DataPropertyName = "MachineName", FillWeight = 80 });

            pnl.Controls.Add(dgvAudit);
            pnl.Controls.Add(lbl);
            tabAudit.Controls.Add(pnl);
        }

        private void BuildUsersTab()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

            var toolbar = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Color.White };

            var btnNewUser = MakeButton("+ New User", 8, 8, Color.FromArgb(30, 80, 160));
            btnNewUser.Click += (s, e) =>
            {
                using var f = new UserForm(_users);
                if (f.ShowDialog(this) == DialogResult.OK) LoadUsersGrid();
            };

            var btnToggleActive = MakeButton("Toggle Active/Inactive", 120, 8, Color.FromArgb(100, 100, 100));
            btnToggleActive.Size = new Size(160, 30);
            btnToggleActive.Click += (s, e) =>
            {
                if (dgvUsers.SelectedRows.Count == 0) return;
                int uid = Convert.ToInt32(dgvUsers.SelectedRows[0].Cells["UId"].Value);
                bool cur = Convert.ToBoolean(dgvUsers.SelectedRows[0].Cells["UIsActive"].Value);
                var (ok, err) = _users.SetActiveStatus(uid, !cur);
                if (!ok) MessageBox.Show(err, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else LoadUsersGrid();
            };

            toolbar.Controls.AddRange(new Control[] { btnNewUser, btnToggleActive });

            dgvUsers = BuildGrid();
            dgvUsers.Dock = DockStyle.Fill;
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "UId", HeaderText = "ID", DataPropertyName = "Id", FillWeight = 30 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "UFullName", HeaderText = "Name", DataPropertyName = "FullName", FillWeight = 140 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "UUsername", HeaderText = "Username", DataPropertyName = "Username", FillWeight = 90 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "UEmail", HeaderText = "Email", DataPropertyName = "Email", FillWeight = 150 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "UDept", HeaderText = "Department", DataPropertyName = "Department", FillWeight = 90 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "URole", HeaderText = "Role", DataPropertyName = "Role", FillWeight = 80 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "UIsActive", HeaderText = "Active", DataPropertyName = "IsActive", FillWeight = 50 });
            dgvUsers.Columns.Add(new DataGridViewTextBoxColumn { Name = "ULastLogin", HeaderText = "Last Login", DataPropertyName = "LastLoginAt", FillWeight = 100 });

            pnl.Controls.Add(dgvUsers);
            pnl.Controls.Add(toolbar);
            tabUsers.Controls.Add(pnl);
        }

        private void BuildSettingsTab()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, Padding = new Padding(16) };
            var lbl = new Label
            {
                Text = "Email settings are stored in the database.\nAdmin can update them here. Restart the app after saving.",
                Font = new Font("Segoe UI", 10F),
                Dock = DockStyle.Top,
                Height = 60,
                ForeColor = Color.FromArgb(60, 60, 80)
            };

            var btnOpenEmail = MakeButton("Configure Email Settings", 0, 70, Color.FromArgb(30, 80, 160));
            btnOpenEmail.Size = new Size(200, 34);
            btnOpenEmail.Click += (s, e) =>
            {
                using var f = new EmailSettingsForm();
                f.ShowDialog(this);
            };

            var btnOpenDb = MakeButton("Open Database Folder", 0, 116, Color.FromArgb(80, 80, 80));
            btnOpenDb.Size = new Size(200, 34);
            btnOpenDb.Click += (s, e) =>
                System.Diagnostics.Process.Start("explorer.exe",
                    Path.GetDirectoryName(AppDbContext.GetDbPath())!);

            pnl.Controls.AddRange(new Control[] { btnOpenEmail, btnOpenDb, lbl });
            tabSettings.Controls.Add(pnl);
        }

        private void BuildStatusBar()
        {
            lblStatus = new ToolStripStatusLabel { Text = "Ready" };
            statusStrip = new StatusStrip();
            statusStrip.Items.Add(lblStatus);
            statusStrip.Items.Add(new ToolStripStatusLabel { Text = $"DB: {AppDbContext.GetDbPath()}", Spring = false });
            statusStrip.BackColor = Color.FromArgb(240, 242, 248);
            this.Controls.Add(statusStrip);
        }

        // ============================================================
        // DATA LOADING
        // ============================================================
        private void LoadData()
        {
            UpdateStats();
            LoadRequestsGrid();
            LoadBalanceGrid();
        }

        private void LoadCurrentTab()
        {
            if (tabMain.SelectedTab == tabRequests) LoadRequestsGrid();
            else if (tabMain.SelectedTab == tabBalance) LoadBalanceGrid();
            else if (tabMain.SelectedTab == tabAudit) LoadAuditGrid();
            else if (tabMain.SelectedTab == tabUsers) LoadUsersGrid();
        }

        private void LoadRequestsGrid()
        {
            string? status = cmbStatus?.SelectedItem?.ToString();
            string? dept = cmbDept?.SelectedItem?.ToString();
            int year = 0;
            if (cmbYear?.SelectedItem is int y) year = y;

            var requests = _leave.GetRequests(
                statusFilter: status == "All" ? null : status,
                departmentFilter: dept == "All Departments" ? null : dept,
                searchName: txtSearch?.Text.Trim(),
                year: year);

            // Project to anonymous type with display-friendly properties
            var display = requests.Select(r => new
            {
                r.Id,
                EmployeeName = r.User?.FullName ?? "",
                Department = r.User?.Department ?? "",
                r.LeaveType,
                r.StartDate,
                r.EndDate,
                r.TotalDays,
                StatusStr = r.Status.ToString(),
                r.SubmittedAt,
                ReviewedByName = r.ReviewedByUser?.FullName ?? "",
                r.ManagerNote
            }).ToList();

            var bs = new BindingSource { DataSource = display };
            dgvRequests.DataSource = bs;

            lblStatus.Text = $"{requests.Count} request(s) shown";
            UpdateStats();
        }

        private void LoadBalanceGrid()
        {
            var balances = _leave.GetBalances(Session.CurrentUser!.Id, DateTime.Today.Year);
            var display = balances.Select(b => new
            {
                b.LeaveType,
                b.TotalEntitlement,
                b.DaysUsed,
                b.DaysPending,
                b.DaysRemaining
            }).ToList();

            var bs = new BindingSource { DataSource = display };
            dgvBalance.DataSource = bs;

            // Color remaining days
            foreach (DataGridViewRow row in dgvBalance.Rows)
            {
                if (row.Cells["DaysRemaining"].Value == null) continue;
                int rem = Convert.ToInt32(row.Cells["DaysRemaining"].Value);
                row.Cells["DaysRemaining"].Style.ForeColor = rem <= 2 ? Color.FromArgb(180, 30, 30) :
                                                             rem <= 5 ? Color.FromArgb(160, 100, 0) :
                                                                        Color.FromArgb(0, 120, 50);
                row.Cells["DaysRemaining"].Style.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            }
        }

        private void LoadAuditGrid()
        {
            var logs = _audit.GetLogs();
            var display = logs.Select(a => new
            {
                TimestampLocal = a.Timestamp.ToLocalTime(),
                ActorName = a.ActorUser?.FullName ?? $"[{a.ActorUserId}]",
                a.Action,
                a.LeaveRequestId,
                a.Details,
                a.MachineName
            }).ToList();

            dgvAudit.DataSource = new BindingSource { DataSource = display };
        }

        private void LoadUsersGrid()
        {
            if (dgvUsers == null) return;
            var users = _users.GetAll(includeInactive: true);
            dgvUsers.DataSource = new BindingSource { DataSource = users };
        }

        private void UpdateStats()
        {
            var (total, pending, approved, rejected) = _leave.GetStats();
            lblTotal.Text = $"Total: {total}";
            lblPending.Text = $"Pending: {pending}";
            lblApproved.Text = $"Approved: {approved}";
            lblRejected.Text = $"Rejected: {rejected}";
        }

        // ============================================================
        // EVENTS
        // ============================================================
        private void BtnSubmit_Click(object? sender, EventArgs e)
        {
            if (cmbLeaveType.SelectedIndex < 0) { ShowError("Select a leave type."); return; }
            if (dtpEnd.Value < dtpStart.Value) { ShowError("End date must be after start date."); return; }

            var (ok, err, req) = _leave.Submit(
                Session.CurrentUser!.Id,
                cmbLeaveType.SelectedItem!.ToString()!,
                dtpStart.Value, dtpEnd.Value,
                txtReason.Text);

            if (!ok) { ShowError(err); return; }

            ShowInfo($"Request submitted! ({req!.TotalDays} working day(s))");
            ClearSubmitForm();
            LoadRequestsGrid();
        }

        private void BtnApprove_Click(object? sender, EventArgs e)
        {
            if (_selectedRequestId < 0) { ShowError("Select a request first."); return; }
            var r = MessageBox.Show("Approve this request?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            var (ok, err) = _leave.Approve(_selectedRequestId, txtMgrNote.Text);
            if (!ok) ShowError(err); else { LoadRequestsGrid(); txtMgrNote.Clear(); }
        }

        private void BtnReject_Click(object? sender, EventArgs e)
        {
            if (_selectedRequestId < 0) { ShowError("Select a request first."); return; }
            if (string.IsNullOrWhiteSpace(txtMgrNote.Text)) { ShowError("A reason is required to reject."); return; }
            var r = MessageBox.Show("Reject this request?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            var (ok, err) = _leave.Reject(_selectedRequestId, txtMgrNote.Text);
            if (!ok) ShowError(err); else { LoadRequestsGrid(); txtMgrNote.Clear(); }
        }

        private void BtnCancel_Click(object? sender, EventArgs e)
        {
            if (_selectedRequestId < 0) { ShowError("Select a request first."); return; }
            var r = MessageBox.Show("Cancel this request?", "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return;
            var (ok, err) = _leave.Cancel(_selectedRequestId);
            if (!ok) ShowError(err); else LoadRequestsGrid();
        }

        private void BtnExport_Click(object? sender, EventArgs e)
        {
            try
            {
                var requests = _leave.GetRequests();
                string path = _export.ExportToExcel(requests);
                if (MessageBox.Show($"Exported to:\n{path}\n\nOpen file now?", "Export Complete",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    System.Diagnostics.Process.Start("explorer.exe", path);
            }
            catch (Exception ex) { ShowError("Export failed: " + ex.Message); }
        }

        private void DgvRequests_SelectionChanged(object? sender, EventArgs e)
        {
            if (dgvRequests.SelectedRows.Count == 0) { _selectedRequestId = -1; return; }
            var row = dgvRequests.SelectedRows[0];
            if (row.Cells["Id"].Value != null)
            {
                _selectedRequestId = Convert.ToInt32(row.Cells["Id"].Value);
                if (txtMgrNote != null && row.Cells["ManagerNote"].Value != null)
                    txtMgrNote.Text = row.Cells["ManagerNote"].Value.ToString() ?? "";
                lblStatus.Text = $"Selected: Request #{_selectedRequestId} — {row.Cells["StatusStr"].Value}";
            }
        }

        private void ColorByStatus(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            var cell = dgvRequests.Rows[e.RowIndex].Cells["StatusStr"];
            if (cell.Value == null) return;
            dgvRequests.Rows[e.RowIndex].DefaultCellStyle.BackColor = cell.Value.ToString() switch
            {
                "Approved" => Color.FromArgb(232, 255, 238),
                "Rejected" => Color.FromArgb(255, 232, 232),
                "Pending" => Color.FromArgb(255, 252, 220),
                "Cancelled" => Color.FromArgb(240, 240, 240),
                _ => Color.White
            };
        }

        private void RecalcDays(object? sender, EventArgs e)
        {
            if (dtpStart == null || dtpEnd == null || lblDaysCalc == null) return;
            if (dtpEnd.Value >= dtpStart.Value)
            {
                int days = LeaveService.CalculateWorkingDays(dtpStart.Value, dtpEnd.Value);
                lblDaysCalc.Text = $"  {days} working day(s) selected";
            }
            else
            {
                lblDaysCalc.Text = "  End date is before start date";
            }
        }

        private void ClearSubmitForm()
        {
            cmbLeaveType.SelectedIndex = -1;
            dtpStart.Value = DateTime.Today;
            dtpEnd.Value = DateTime.Today;
            txtReason.Clear();
            lblDaysCalc.Text = "";
        }

        // ============================================================
        // HELPERS
        // ============================================================
        private static DataGridView BuildGrid() => new DataGridView
        {
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            EnableHeadersVisualStyles = false,
            ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Color.FromArgb(30, 80, 160),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            },
            RowTemplate = { Height = 28 },
            Font = new Font("Segoe UI", 9F),
            GridColor = Color.FromArgb(220, 225, 235),
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal
        };

        private static Label MakeLabel(string text, int x, int y) => new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true
        };

        private static Button MakeButton(string text, int x, int y,
            Color bg, Color? fg = null, int buttonHeight = 34) => new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(116, buttonHeight),
                BackColor = bg,
                ForeColor = fg ?? Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };

        private static void ShowError(string msg) =>
            MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);

        private static void ShowInfo(string msg) =>
            MessageBox.Show(msg, "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}