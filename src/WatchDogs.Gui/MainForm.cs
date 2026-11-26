using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using WatchDogs.Core;

namespace WatchDogs.Gui
{
    public sealed class MainForm : Form
    {
        // --- status ---
        private Label _lblMonitorService;
        private Label _lblEngineState;
        private Label _lblMonitorHint;
        private DataGridView _gridStatus;
        private ToolStripStatusLabel _statusLabel;

        // --- settings: multi-service ---
        private ListBox _listServices;
        private ComboBox _cbTargetService;
        private TextBox _txtDisplayName;
        private ComboBox _cbCheckType;
        private TextBox _txtTcpHost;
        private NumericUpDown _numTcpPort;
        private TextBox _txtHttpUrl;
        private NumericUpDown _numInterval;
        private NumericUpDown _numTimeout;
        private CheckBox _chkAutoRestart;
        private NumericUpDown _numMaxRestarts;
        private NumericUpDown _numCooldown;
        private CheckBox _chkSvcEnabled;
        private NumericUpDown _numBootDelay;
        private Button _btnSave;
        private Button _btnDeleteSvc;
        private Button _btnAddSvc;
        private Panel _detailPanel;

        // --- log ---
        private TextBox _txtLog;

        private Timer _refreshTimer;
        private StatusStrip _statusStrip;

        // in-memory working copy of config
        private AppConfig _cfg;

        public MainForm()
        {
            Text = "Watch Dogs — мониторинг служб 1С";
            Width = 1040;
            Height = 740;
            MinimumSize = new Size(900, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;

            ApplyBackgroundIcon();

            var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(12, 6) };

            var tabStatus = new TabPage("Состояние");
            BuildStatusTab(tabStatus);

            var tabSettings = new TabPage("Настройки");
            BuildSettingsTab(tabSettings);

            var tabLog = new TabPage("Журнал");
            BuildLogTab(tabLog);

            tabs.TabPages.Add(tabStatus);
            tabs.TabPages.Add(tabSettings);
            tabs.TabPages.Add(tabLog);

            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Готово");
            _statusLabel.Spring = true;
            _statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            var devLabel = new ToolStripStatusLabel("csl.by · +375 29 337-14-12");
            devLabel.IsLink = true;
            devLabel.LinkBehavior = LinkBehavior.HoverUnderline;
            devLabel.Click += (s, e) =>
            {
                try { Process.Start("http://csl.by"); } catch { /* ignore */ }
            };
            _statusStrip.Items.Add(_statusLabel);
            _statusStrip.Items.Add(devLabel);

            Controls.Add(tabs);
            Controls.Add(_statusStrip);

            _refreshTimer = new Timer { Interval = 3000 };
            _refreshTimer.Tick += (s, e) => RefreshStatus();
            _refreshTimer.Start();

            Load += (s, e) =>
            {
                LoadConfigUi();
                RefreshStatus();
                LoadLog();
                LoadServiceListCombo();
            };

            FormClosed += (s, e) => _refreshTimer.Stop();
        }

        // =====================================================================
        // Background watermark from application icon
        // =====================================================================

        private void ApplyBackgroundIcon()
        {
            try
            {
                var icon = LoadEmbeddedIcon();
                if (icon == null)
                    return;

                BackColor = Color.White;
                BackgroundImage = CreateWatermark(icon, 900, 700);
                BackgroundImageLayout = ImageLayout.Tile;
                icon.Dispose();
            }
            catch
            {
                // background is cosmetic — ignore failures
            }
        }

        private static Image LoadEmbeddedIcon()
        {
            var asm = Assembly.GetExecutingAssembly();
            var names = asm.GetManifestResourceNames();
            var name = names.FirstOrDefault(n => n.EndsWith("watchdogs-icon.png", StringComparison.OrdinalIgnoreCase));
            if (name == null)
                return null;
            using (var s = asm.GetManifestResourceStream(name))
            {
                if (s == null)
                    return null;
                using (var tmp = new Bitmap(s))
                {
                    return new Bitmap(tmp);
                }
            }
        }

        /// <summary>Faded watermark tile so UI stays readable.</summary>
        private static Image CreateWatermark(Image icon, int w, int h)
        {
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;

                // soft rounded panel behind content areas
                using (var brush = new SolidBrush(Color.FromArgb(18, 0, 90, 160)))
                {
                    g.FillRectangle(brush, 0, 0, w, h);
                }

                // large faded icon bottom-right
                var size = Math.Min(w, h) * 55 / 100;
                var x = w - size - 24;
                var y = h - size - 48;
                var cm = new ColorMatrix { Matrix33 = 0.10f }; // 10% opacity
                using (var ia = new ImageAttributes())
                {
                    ia.SetColorMatrix(cm);
                    g.DrawImage(icon, new Rectangle(x, y, size, size),
                        0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, ia);
                }

                // smaller mark top-right
                var size2 = 72;
                using (var ia = new ImageAttributes())
                {
                    var cm2 = new ColorMatrix { Matrix33 = 0.16f };
                    ia.SetColorMatrix(cm2);
                    g.DrawImage(icon, new Rectangle(w - size2 - 16, 12, size2, size2),
                        0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, ia);
                }
            }
            return bmp;
        }

        // =====================================================================
        // Status tab
        // =====================================================================

        private void BuildStatusTab(TabPage tab)
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 6,
                BackColor = Color.FromArgb(252, 252, 254)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = "Watch Dogs — состояние мониторинга",
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 2)
            };
            root.Controls.Add(title, 0, 0);
            root.SetColumnSpan(title, 2);

            var devLine = new Label
            {
                Text = "Разработчик: csl.by  ·  +375 29 337-14-12",
                AutoSize = true,
                ForeColor = Color.SteelBlue,
                Margin = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(devLine, 0, 1);
            root.SetColumnSpan(devLine, 2);

            _lblMonitorHint = new Label
            {
                AutoSize = false,
                Height = 36,
                Text = "",
                ForeColor = Color.DarkRed,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(_lblMonitorHint, 0, 2);
            root.SetColumnSpan(_lblMonitorHint, 2);

            _lblMonitorService = MakeInfoLabel();
            _lblEngineState = MakeInfoLabel();
            AddStatusRow(root, 3, "Служба мониторинга:", _lblMonitorService);
            // put engine on same visual block — use row 3 second column via nested? keep simple:
            // Actually AddStatusRow uses two rows; adjust:
            // We'll put engine state into monitor hint area on refresh instead.

            _gridStatus = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                Margin = new Padding(0, 8, 0, 8)
            };
            _gridStatus.Columns.Add("svc", "Служба 1С");
            _gridStatus.Columns.Add("st", "Статус Windows");
            _gridStatus.Columns.Add("ok", "Отвечает");
            _gridStatus.Columns.Add("state", "Движок");
            _gridStatus.Columns.Add("msg", "Сообщение");
            _gridStatus.Columns.Add("cnt", "Проверок / перезапусков");
            _gridStatus.Columns.Add("time", "Проверка (UTC)");
            _gridStatus.Columns["svc"].FillWeight = 22;
            _gridStatus.Columns["st"].FillWeight = 12;
            _gridStatus.Columns["ok"].FillWeight = 10;
            _gridStatus.Columns["state"].FillWeight = 12;
            _gridStatus.Columns["msg"].FillWeight = 28;
            _gridStatus.Columns["cnt"].FillWeight = 16;
            _gridStatus.Columns["time"].FillWeight = 18;

            root.Controls.Add(_gridStatus, 0, 4);
            root.SetColumnSpan(_gridStatus, 2);

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                Padding = new Padding(8, 6, 8, 4),
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.FromArgb(252, 252, 254)
            };

            void AddBtn(string text, EventHandler onClick)
            {
                var b = new Button
                {
                    Text = text,
                    AutoSize = true,
                    Padding = new Padding(10, 3, 10, 3),
                    Margin = new Padding(0, 0, 8, 0)
                };
                b.Click += onClick;
                btnPanel.Controls.Add(b);
            }

            AddBtn("🔄 Обновить", (s, e) => RefreshStatus());
            AddBtn("⬇ Установить службу мониторинга", async (s, e) => await InstallMonitoringServiceAsync());
            AddBtn("▶ Запустить", async (s, e) => await ControlMonitoringServiceAsync(true));
            AddBtn("⏸ Остановить", async (s, e) => await ControlMonitoringServiceAsync(false));
            AddBtn("🗑 Удалить службу мониторинга", async (s, e) => await UninstallMonitoringServiceAsync());
            AddBtn("📁 Открыть логи", (s, e) => OpenLogFolder());

            tab.BackColor = Color.FromArgb(252, 252, 254);
            tab.Controls.Add(root);
            tab.Controls.Add(btnPanel);
        }

        private static void AddStatusRow(TableLayoutPanel root, int row, string caption, Label value)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 8, 0)
            };
            lbl.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);

            value.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            value.Margin = new Padding(0, 4, 0, 4);

            root.Controls.Add(lbl, 0, row);
            root.Controls.Add(value, 1, row);
        }

        private static Label MakeInfoLabel()
        {
            return new Label
            {
                AutoSize = false,
                Height = 22,
                Text = "—",
                ForeColor = Color.FromArgb(30, 30, 30)
            };
        }

        // =====================================================================
        // Settings tab — multi-service
        // =====================================================================

        private void BuildSettingsTab(TabPage tab)
        {
            tab.BackColor = Color.FromArgb(252, 252, 254);

            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 0),
                ColumnCount = 1,
                RowCount = 5,
                BackColor = Color.FromArgb(252, 252, 254)
            };
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var hint = new Label
            {
                Text = "💡 Каждая служба 1С — отдельный профиль контроля. Нажмите «Сохранить настройки», " +
                       "и служба мониторинга перечитает конфигурацию при следующей проверке. " +
                       "Задержка после загрузки — общая для всех. ПКМ по списку — удалить/отключить.",
                AutoSize = true,
                MaximumSize = new Size(980, 0),
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 0, 0, 8)
            };
            outer.Controls.Add(hint, 0, 0);

            // ---- top: global boot delay ----
            var globalPanel = new FlowLayoutPanel
            {
                AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0, 0, 0, 8),
                BackColor = Color.FromArgb(240, 246, 252)
            };
            globalPanel.Controls.Add(new Label
            {
                Text = "Задержка после загрузки системы (мин):",
                AutoSize = true,
                Margin = new Padding(8, 8, 8, 8)
            });
            _numBootDelay = MakeNum(0, 1440, 5);
            _numBootDelay.Margin = new Padding(0, 4, 16, 4);
            globalPanel.Controls.Add(_numBootDelay);
            globalPanel.Controls.Add(new Label
            {
                Text = "— общая: сначала ждём этот интервал, затем проверяем все службы из списка.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 8, 8, 8)
            });
            outer.Controls.Add(globalPanel, 0, 1);

            // ---- middle: split list | detail ----
            // Never set Panel1MinSize/Panel2MinSize here: SplitContainer is not
            // sized yet and throws InvalidOperationException (startup crash).
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 300,
                FixedPanel = FixedPanel.Panel1,
                Margin = new Padding(0)
            };

            // left: service list
            var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 8, 0) };
            var listTitle = new Label
            {
                Text = "Службы в мониторинге:",
                Dock = DockStyle.Top,
                Height = 24,
                Font = new Font(Font, FontStyle.Bold)
            };
            _listServices = new ListBox
            {
                Dock = DockStyle.Fill,
                DisplayMember = "Title",
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle
            };
            _listServices.SelectedIndexChanged += (s, e) => ShowSelectedService();

            // right-click: delete / disable selected service
            var listMenu = new ContextMenuStrip();
            var miDelete = new ToolStripMenuItem("🗑 Удалить из мониторинга");
            miDelete.Click += (s, e) => DeleteSelectedService();
            var miToggle = new ToolStripMenuItem("⏸ Отключить / включить");
            miToggle.Click += (s, e) => ToggleSelectedService();
            listMenu.Items.Add(miDelete);
            listMenu.Items.Add(miToggle);
            _listServices.ContextMenuStrip = listMenu;
            _listServices.MouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                var idx = _listServices.IndexFromPoint(e.Location);
                if (idx >= 0) _listServices.SelectedIndex = idx;
            };

            var listBtns = new TableLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 76,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 6, 0, 0)
            };
            listBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            listBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            listBtns.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            _btnAddSvc = new Button
            {
                Text = "➕ Добавить",
                Dock = DockStyle.Fill,
                Height = 30,
                Margin = new Padding(0, 0, 4, 0)
            };
            _btnAddSvc.Click += (s, e) => AddService();

            _btnDeleteSvc = new Button
            {
                Text = "🗑 Удалить",
                Dock = DockStyle.Fill,
                Height = 30,
                Margin = new Padding(4, 0, 0, 0),
                ForeColor = Color.Firebrick
            };
            _btnDeleteSvc.Click += (s, e) => DeleteSelectedService();

            var listTip = new ToolTip { InitialDelay = 400, ShowAlways = true };
            listTip.SetToolTip(_btnAddSvc, "Добавить новую службу 1С в список мониторинга");
            listTip.SetToolTip(_btnDeleteSvc, "Убрать выбранную службу из мониторинга (Windows-служба не удаляется)");
            listTip.SetToolTip(_listServices, "ЛКМ — выбрать · ПКМ — удалить или отключить");

            listBtns.Controls.Add(_btnAddSvc, 0, 0);
            listBtns.Controls.Add(_btnDeleteSvc, 1, 0);

            left.Controls.Add(_listServices);
            left.Controls.Add(listBtns);
            left.Controls.Add(listTitle);

            // right: detail form
            _detailPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(8, 0, 0, 0)
            };
            BuildDetailControls(_detailPanel);

            split.Panel1.Controls.Add(left);
            split.Panel2.Controls.Add(_detailPanel);
            outer.Controls.Add(split, 0, 2);

            // ---- save bar ----
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(0, 10, 0, 6),
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnSave = MakeActionButton("💾 Сохранить настройки", async (s, e) => await SaveSettingsAsync(), true);
            var btnReload = MakeActionButton("🔄 Загрузить текущие", (s, e) => LoadConfigUi(), false);
            var btnTest = MakeActionButton("🩺 Проверить выбранную", async (s, e) => await TestCheckAsync(), false);
            var btnRestart = MakeActionButton("🔁 Перезапустить выбранную", async (s, e) => await RestartFromSettingsAsync(), false);

            btnPanel.Controls.Add(_btnSave);
            btnPanel.Controls.Add(btnReload);
            btnPanel.Controls.Add(btnTest);
            btnPanel.Controls.Add(btnRestart);
            outer.Controls.Add(btnPanel, 0, 3);

            // developer footer
            var foot = new Label
            {
                Text = "csl.by · +375 29 337-14-12",
                AutoSize = true,
                ForeColor = Color.SteelBlue,
                Margin = new Padding(0, 4, 0, 8)
            };
            outer.Controls.Add(foot, 0, 4);

            tab.Controls.Add(outer);
        }

        private void BuildDetailControls(Panel parent)
        {
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(0, 4, 0, 4)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 240));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            int row = 0;

            void Row(string caption, Control c)
            {
                while (tlp.RowStyles.Count <= row)
                    tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                var lbl = new Label
                {
                    Text = caption,
                    AutoSize = true,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(0, 7, 8, 0)
                };
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                c.Margin = new Padding(0, 4, 0, 4);
                tlp.Controls.Add(lbl, 0, row);
                tlp.Controls.Add(c, 1, row);
                row++;
            }

            _txtDisplayName = new TextBox { Dock = DockStyle.Fill };
            Row("Отображаемое имя:", _txtDisplayName);

            _cbTargetService = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Dock = DockStyle.Fill
            };
            var pickPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            _cbTargetService.Width = 320;
            var btnPick = new Button { Text = "Из списка…", AutoSize = true, Padding = new Padding(10, 2, 10, 2) };
            btnPick.Click += (s, e) => PickService();
            pickPanel.Controls.Add(_cbTargetService);
            pickPanel.Controls.Add(btnPick);
            Row("Служба Windows (Name):", pickPanel);

            _cbCheckType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
            _cbCheckType.Items.AddRange(new object[]
            {
                "Статус службы (Running)",
                "Статус + TCP-порт",
                "Статус + HTTP-адрес"
            });
            Row("Тип проверки:", _cbCheckType);

            _txtTcpHost = new TextBox { Dock = DockStyle.Fill, Text = "localhost" };
            Row("TCP хост:", _txtTcpHost);

            _numTcpPort = MakeNum(1, 65535, 1541);
            _numTcpPort.Width = 120;
            Row("TCP порт:", _numTcpPort);

            _txtHttpUrl = new TextBox { Dock = DockStyle.Fill, Text = "http://localhost/health" };
            Row("HTTP URL:", _txtHttpUrl);

            _numInterval = MakeNum(5, 86400, 30);
            Row("Интервал проверки (сек):", _numInterval);

            _numTimeout = MakeNum(1, 300, 10);
            Row("Таймаут ответа (сек):", _numTimeout);

            _chkSvcEnabled = new CheckBox { Text = "Мониторинг этой службы включён", AutoSize = true, Checked = true };
            Row("Включение:", _chkSvcEnabled);

            _chkAutoRestart = new CheckBox
            {
                Text = "Автоперезапуск при отказе",
                AutoSize = true,
                Checked = true
            };
            Row("Перезапуск:", _chkAutoRestart);

            _numMaxRestarts = MakeNum(1, 100, 5);
            Row("Макс. перезапусков подряд:", _numMaxRestarts);

            _numCooldown = MakeNum(0, 1440, 5);
            Row("Пауза между перезапусками (мин):", _numCooldown);

            parent.Controls.Add(tlp);
        }

        private static GroupBox MakeGroup(string text)
        {
            return new GroupBox
            {
                Text = text,
                Dock = DockStyle.Fill,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(10, 6, 10, 8),
                Margin = new Padding(0, 0, 0, 8)
            };
        }

        private static NumericUpDown MakeNum(decimal min, decimal max, decimal val)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = val,
                Width = 120,
                Margin = new Padding(0, 4, 0, 4)
            };
        }

        private static Button MakeActionButton(string text, EventHandler onClick, bool primary)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                Padding = new Padding(12, 4, 12, 4),
                Margin = new Padding(0, 0, 8, 0)
            };
            if (primary)
                b.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
            b.Click += onClick;
            return b;
        }

        // =====================================================================
        // Log tab
        // =====================================================================

        private void BuildLogTab(TabPage tab)
        {
            tab.BackColor = Color.FromArgb(252, 252, 254);
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8), BackColor = Color.White };

            _txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = new Font("Consolas", 9F),
                BackColor = Color.White
            };

            var logButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                Padding = new Padding(4),
                FlowDirection = FlowDirection.LeftToRight
            };
            var btnRefreshLog = new Button { Text = "Обновить журнал", AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
            btnRefreshLog.Click += (s, e) => LoadLog();
            var btnOpenLogDir = new Button { Text = "Открыть папку", AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
            btnOpenLogDir.Click += (s, e) => OpenLogFolder();
            logButtons.Controls.Add(btnRefreshLog);
            logButtons.Controls.Add(btnOpenLogDir);

            panel.Controls.Add(_txtLog);
            panel.Controls.Add(logButtons);
            tab.Controls.Add(panel);
        }

        // =====================================================================
        // Status logic
        // =====================================================================

        private void RefreshStatus()
        {
            try
            {
                var monitorStatus = ServiceInstallerHelper.GetStatusText();
                _lblMonitorService.Text = monitorStatus;

                var running = string.Equals(monitorStatus, "Running", StringComparison.OrdinalIgnoreCase);
                _lblMonitorService.ForeColor = running ? Color.Green : Color.Firebrick;

                if (running)
                {
                    _lblMonitorHint.Text = "";
                }
                else if (monitorStatus == "NOT_INSTALLED")
                {
                    _lblMonitorHint.Text =
                        "⚠ Служба мониторинга НЕ установлена — автоперезапуск не работает. «Установить службу».";
                }
                else
                {
                    _lblMonitorHint.Text =
                        "⚠ Служба мониторинга не запущена (" + monitorStatus + "). «Запустить мониторинг».";
                }

                var st = ConfigStore.LoadStatus();
                _gridStatus.Rows.Clear();

                if (st == null || st.Services == null || st.Services.Count == 0)
                {
                    if (st != null && !string.IsNullOrEmpty(st.TargetServiceName))
                    {
                        // legacy single-service status
                        _gridStatus.Rows.Add(
                            st.TargetServiceName,
                            st.TargetServiceStatus ?? "—",
                            st.LastCheckHealthy ? "OK" : "FAIL",
                            st.State ?? "—",
                            st.LastCheckMessage ?? "—",
                            st.TotalChecks + " / " + st.TotalRestarts,
                            st.LastCheckTimeUtc ?? "—");
                    }
                    else
                    {
                        _gridStatus.Rows.Add(
                            "—", "—", "—", "—",
                            "Нет данных. Сохраните настройки и запустите мониторинг.",
                            "0 / 0", "—");
                        _lblEngineState.Text = "нет данных";
                    }
                    return;
                }

                _lblEngineState.Text = st.State + (string.IsNullOrEmpty(st.Message) ? "" : " — " + st.Message);

                foreach (var s in st.Services)
                {
                    var r = _gridStatus.Rows.Add(
                        string.IsNullOrWhiteSpace(s.DisplayName)
                            ? s.TargetServiceName
                            : s.DisplayName + " (" + s.TargetServiceName + ")",
                        s.TargetServiceStatus ?? "—",
                        s.Enabled ? (s.LastCheckHealthy ? "OK" : "FAIL") : "—",
                        s.State ?? "—",
                        s.LastCheckMessage ?? "—",
                        s.TotalChecks + " / " + s.TotalRestarts,
                        s.LastCheckTimeUtc ?? "—");

                    var row = _gridStatus.Rows[r];
                    if (s.Enabled && s.State != "Disabled")
                    {
                        row.Cells[2].Style.ForeColor = s.LastCheckHealthy ? Color.Green : Color.Firebrick;
                        if (!s.LastCheckHealthy)
                            row.Cells[2].Style.Font = new Font(_gridStatus.Font, FontStyle.Bold);
                    }
                }

                // fold engine state into hint line when monitor is running
                if (running)
                {
                    _lblMonitorHint.Text = "Движок: " + st.State +
                                           (string.IsNullOrEmpty(st.Message) ? "" : " — " + st.Message);
                    _lblMonitorHint.ForeColor = Color.DarkGreen;
                }
                else
                {
                    _lblMonitorHint.ForeColor = Color.DarkRed;
                }
            }
            catch (Exception ex)
            {
                SetStatus("Ошибка обновления: " + ex.Message);
            }
        }

        private async Task ControlMonitoringServiceAsync(bool start)
        {
            try
            {
                SetStatus(start ? "Запуск службы мониторинга…" : "Остановка службы мониторинга…");
                var ok = await Task.Run(() =>
                {
                    string msg;
                    var success = start ? ServiceInstallerHelper.Start(out msg) : ServiceInstallerHelper.Stop(out msg);
                    return new { success, msg };
                });
                SetStatus(ok.msg);
                if (!ok.success)
                    MessageBox.Show(this, ok.msg, "Watch Dogs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task InstallMonitoringServiceAsync()
        {
            try
            {
                var result = await Task.Run(() =>
                {
                    string msg;
                    var ok = ServiceInstallerHelper.Install(out msg);
                    if (ok)
                    {
                        string sMsg;
                        ServiceInstallerHelper.Start(out sMsg);
                        msg += ". " + sMsg;
                    }
                    return new { ok, msg };
                });
                SetStatus(result.msg);
                MessageBox.Show(this, result.msg, "Установка службы", MessageBoxButtons.OK,
                    result.ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task UninstallMonitoringServiceAsync()
        {
            var confirm = MessageBox.Show(this,
                "Удалить службу мониторинга WatchDogsMonitor?\n\n" +
                "Программа и настройки останутся. Полное удаление — «Установка и удаление программ».",
                "Удаление службы",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            try
            {
                var result = await Task.Run(() =>
                {
                    string msg;
                    var ok = ServiceInstallerHelper.Uninstall(out msg);
                    return new { ok, msg };
                });
                SetStatus(result.msg);
                MessageBox.Show(this, result.msg, "Удаление службы", MessageBoxButtons.OK,
                    result.ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenLogFolder()
        {
            try
            {
                Paths.EnsureDirectories();
                Process.Start("explorer.exe", Paths.LogDirectory);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =====================================================================
        // Multi-service settings logic
        // =====================================================================

        private void LoadConfigUi()
        {
            try
            {
                _cfg = ConfigStore.LoadConfig();
                if (_cfg.Services == null || _cfg.Services.Count == 0)
                    _cfg = AppConfig.CreateDefault();
                _cfg.Normalize();

                _numBootDelay.Value = Clamp(_cfg.BootDelayMinutes, 0, 1440);
                RefreshServiceList(0);
                ShowSelectedService();
                SetStatus("Настройки загружены (" + _cfg.Services.Count + " служб)");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось загрузить настройки:\n" + ex.Message,
                    "Watch Dogs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshServiceList(int selectedIndex)
        {
            _listServices.BeginUpdate();
            _listServices.Items.Clear();
            foreach (var s in _cfg.Services)
                _listServices.Items.Add(s);
            _listServices.EndUpdate();

            if (_listServices.Items.Count == 0)
            {
                var def = ServiceConfig.CreateDefault();
                _cfg.Services.Add(def);
                _listServices.Items.Add(def);
            }

            if (selectedIndex < 0 || selectedIndex >= _listServices.Items.Count)
                selectedIndex = 0;
            _listServices.SelectedIndex = selectedIndex;
        }

        private ServiceConfig SelectedService
        {
            get { return _listServices.SelectedItem as ServiceConfig; }
        }

        private void ShowSelectedService()
        {
            var svc = SelectedService;
            SetDetailEnabled(svc != null);
            if (svc == null)
                return;

            // Always fill the detail form from the model — otherwise the form
            // stays empty and "Save" wipes Service Name.
            _txtDisplayName.Text = svc.DisplayName ?? "";
            _cbTargetService.Text = svc.TargetServiceName ?? "";
            _cbCheckType.SelectedIndex = Math.Max(0, Math.Min(2, (int)svc.CheckType));
            _txtTcpHost.Text = svc.TcpHost ?? "localhost";
            _numTcpPort.Value = Clamp(svc.TcpPort, 1, 65535);
            _txtHttpUrl.Text = svc.HttpUrl ?? "";
            _numInterval.Value = Clamp(svc.CheckIntervalSeconds, 5, 86400);
            _numTimeout.Value = Clamp(svc.ResponseTimeoutSeconds, 1, 300);
            _chkAutoRestart.Checked = svc.AutoRestart;
            _numMaxRestarts.Value = Clamp(svc.MaxRestartAttempts, 1, 100);
            _numCooldown.Value = Clamp(svc.RestartCooldownMinutes, 0, 1440);
            _chkSvcEnabled.Checked = svc.MonitoringEnabled;
        }

        private void SetDetailEnabled(bool enabled)
        {
            foreach (Control c in _detailPanel.Controls)
                c.Enabled = enabled;
            // also children of table layout
            foreach (Control c in _detailPanel.Controls)
            {
                foreach (Control cc in c.Controls)
                    cc.Enabled = enabled;
            }
            _btnDeleteSvc.Enabled = enabled;
        }

        private void ApplyDetailToSelected()
        {
            var svc = SelectedService;
            if (svc == null)
                return;

            svc.DisplayName = _txtDisplayName.Text;
            svc.TargetServiceName = (_cbTargetService.Text ?? "").Trim();
            svc.CheckType = (HealthCheckType)Math.Max(0, Math.Min(2, _cbCheckType.SelectedIndex));
            svc.TcpHost = _txtTcpHost.Text;
            svc.TcpPort = (int)_numTcpPort.Value;
            svc.HttpUrl = _txtHttpUrl.Text;
            svc.CheckIntervalSeconds = (int)_numInterval.Value;
            svc.ResponseTimeoutSeconds = (int)_numTimeout.Value;
            svc.AutoRestart = _chkAutoRestart.Checked;
            svc.MaxRestartAttempts = (int)_numMaxRestarts.Value;
            svc.RestartCooldownMinutes = (int)_numCooldown.Value;
            svc.MonitoringEnabled = _chkSvcEnabled.Checked;
            svc.Normalize();

            var idx = _listServices.SelectedIndex;
            _listServices.Refresh();
            if (idx >= 0 && idx < _listServices.Items.Count)
                _listServices.SelectedIndex = idx;
        }

        private void AddService()
        {
            ApplyDetailToSelected();
            var n = _cfg.Services.Count + 1;
            var svc = ServiceConfig.CreateDefault("1CV8S");
            svc.DisplayName = "Новая служба " + n;
            _cfg.Services.Add(svc);
            RefreshServiceList(_cfg.Services.Count - 1);
            _cbTargetService.Focus();
            SetStatus("Добавлена служба — укажите Service Name и сохраните");
        }

        private void DeleteSelectedService()
        {
            var svc = SelectedService;
            if (svc == null)
            {
                SetStatus("Сначала выберите службу в списке слева");
                return;
            }

            if (_cfg.Services.Count <= 1)
            {
                MessageBox.Show(this,
                    "Нужно оставить хотя бы одну службу.\n" +
                    "Вместо удаления можно отключить её галочкой «Мониторинг этой службы включён» " +
                    "или пунктом меню «Отключить / включить».",
                    "Watch Dogs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var r = MessageBox.Show(this,
                "Убрать службу из мониторинга?\n\n" +
                "  " + svc.Title + "\n  (" + (svc.TargetServiceName ?? "?") + ")\n\n" +
                "Сама Windows-служба 1С НЕ будет удалена — только профиль контроля.\n" +
                "Изменение применится после «Сохранить настройки».",
                "Удаление из мониторинга",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes)
                return;

            var idx = _listServices.SelectedIndex;
            _cfg.Services.Remove(svc);
            RefreshServiceList(Math.Max(0, idx - 1));
            SetStatus("Удалено: " + svc.Title + " — не забудьте «Сохранить настройки»");
            Log.Info("GUI: удалён профиль мониторинга " + svc.Title);
        }

        private void ToggleSelectedService()
        {
            var svc = SelectedService;
            if (svc == null)
                return;
            svc.MonitoringEnabled = !svc.MonitoringEnabled;
            ShowSelectedService();
            _listServices.Refresh();
            SetStatus((svc.MonitoringEnabled ? "Включён" : "Отключён") +
                      " мониторинг: " + svc.Title + " — не забудьте «Сохранить настройки»");
        }

        private void LoadServiceListCombo()
        {
            try
            {
                var names = Task.Run(() => HealthChecker.GetInstalledServiceNames()).GetAwaiter().GetResult();
                var current = _cbTargetService.Text;
                _cbTargetService.Items.Clear();
                foreach (var n in names)
                    _cbTargetService.Items.Add(n);
                if (!string.IsNullOrEmpty(current))
                    _cbTargetService.Text = current;
            }
            catch
            {
                // editable combo still works
            }
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                ApplyDetailToSelected();

                if (_cfg.Services.Count == 0)
                {
                    MessageBox.Show(this, "Добавьте хотя бы одну службу.", "Watch Dogs",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                for (int i = 0; i < _cfg.Services.Count; i++)
                {
                    var s = _cfg.Services[i];
                    if (string.IsNullOrWhiteSpace(s.TargetServiceName))
                    {
                        _listServices.SelectedIndex = i;
                        ShowSelectedService();
                        _cbTargetService.Focus();
                        MessageBox.Show(this,
                            "У службы #" + (i + 1) + " («" + s.Title + "») не задан Service Name.\n\n" +
                            "Выберите службу Windows в поле «Служба Windows (Name)» " +
                            "или нажмите «Из списка…».",
                            "Watch Dogs — не хватает данных",
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                _cfg.BootDelayMinutes = (int)_numBootDelay.Value;
                _cfg.Normalize();

                await Task.Run(() => ConfigStore.SaveConfig(_cfg));
                SetStatus("Настройки сохранены: " + _cfg.Services.Count + " служб → " + Paths.ConfigPath);
                Log.Info("Настройки сохранены GUI. Служб: " + _cfg.Services.Count);

                var monitorStatus = ServiceInstallerHelper.GetStatusText();
                if (monitorStatus == "NOT_INSTALLED")
                {
                    var r = MessageBox.Show(this,
                        "Настройки сохранены (" + _cfg.Services.Count + " служб).\n\n" +
                        "Служба мониторинга НЕ установлена — автоперезапуск работать не будет.\n\n" +
                        "Установить и запустить её сейчас?",
                        "Watch Dogs", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes)
                    {
                        await InstallMonitoringServiceAsync();
                        RefreshStatus();
                        return;
                    }
                }
                else if (!string.Equals(monitorStatus, "Running", StringComparison.OrdinalIgnoreCase))
                {
                    var r = MessageBox.Show(this,
                        "Настройки сохранены.\n\nСлужба мониторинга: " + monitorStatus +
                        ".\nЗапустить её?",
                        "Watch Dogs", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes)
                    {
                        await ControlMonitoringServiceAsync(true);
                        RefreshStatus();
                        return;
                    }
                }

                MessageBox.Show(this,
                    "Настройки сохранены.\nСлужб в мониторинге: " + _cfg.Services.Count +
                    "\n\nФайл: " + Paths.ConfigPath,
                    "Watch Dogs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private ServiceConfig CollectForTest()
        {
            ApplyDetailToSelected();
            var svc = SelectedService;
            if (svc == null || string.IsNullOrWhiteSpace(svc.TargetServiceName))
                throw new InvalidOperationException("Выберите службу и укажите Service Name.");
            _cfg.BootDelayMinutes = (int)_numBootDelay.Value;
            return svc;
        }

        private async Task TestCheckAsync()
        {
            try
            {
                var svc = CollectForTest();
                _btnSave.Enabled = false;
                var result = await Task.Run(() => HealthChecker.Check(svc));

                if (result.IsHealthy)
                {
                    MessageBox.Show(this,
                        "OK: " + result.Message + "\nСостояние: " + result.ServiceStatus,
                        "Проверка", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var answer = MessageBox.Show(this,
                    "FAIL: " + result.Message +
                    "\nСостояние: " + result.ServiceStatus +
                    "\n\nПерезапустить службу сейчас?",
                    "Проверка", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (answer == DialogResult.Yes)
                    await RestartTargetServiceAsync(svc);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка проверки", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnSave.Enabled = true;
            }
        }

        private async Task RestartTargetServiceAsync(ServiceConfig cfg)
        {
            try
            {
                SetStatus("Перезапуск '" + cfg.TargetServiceName + "'…");
                var outcome = await Task.Run(() =>
                {
                    string msg;
                    var ok = HealthChecker.TryRestart(cfg, out msg);
                    return new { ok, msg };
                });
                SetStatus(outcome.msg);
                MessageBox.Show(this, outcome.msg,
                    outcome.ok ? "Перезапуск выполнен" : "Ошибка перезапуска",
                    MessageBoxButtons.OK, outcome.ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка перезапуска", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RestartFromSettingsAsync()
        {
            ServiceConfig svc;
            try
            {
                svc = CollectForTest();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Watch Dogs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(this,
                "Перезапустить службу\n\"" + svc.Title + "\"?\n\nСейчас: " +
                HealthChecker.GetServiceStatusText(svc.TargetServiceName),
                "Перезапуск службы 1С",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            await RestartTargetServiceAsync(svc);
        }

        private void PickService()
        {
            using (var dlg = new ServicePickerForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedService))
                    _cbTargetService.Text = dlg.SelectedService;
            }
        }

        // =====================================================================
        // Logs
        // =====================================================================

        private void LoadLog()
        {
            try
            {
                _txtLog.Text = Log.ReadTail(500);
                _txtLog.SelectionStart = _txtLog.TextLength;
                _txtLog.ScrollToCaret();
            }
            catch (Exception ex)
            {
                _txtLog.Text = "Ошибка: " + ex.Message;
            }
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null)
                _statusLabel.Text = text;
        }

        private static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }

    /// <summary>Modal dialog to pick an installed Windows service.</summary>
    internal sealed class ServicePickerForm : Form
    {
        private readonly ListBox _list;
        private readonly TextBox _filter;
        private List<string> _fullList;

        public string SelectedService { get; private set; }

        public ServicePickerForm()
        {
            Text = "Выбор службы Windows";
            Width = 520;
            Height = 560;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            Font = new Font("Segoe UI", 9F);

            var top = new Panel { Dock = DockStyle.Top, Height = 32, Padding = new Padding(4) };
            _filter = new TextBox { Dock = DockStyle.Fill };
            top.Controls.Add(_filter);

            _list = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9.5F) };

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 48, Padding = new Padding(8) };
            var ok = new Button
            {
                Text = "Выбрать",
                DialogResult = DialogResult.OK,
                Width = 110,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            var cancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Width = 110,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);
            bottom.Resize += (s, e) =>
            {
                ok.Left = bottom.ClientSize.Width - ok.Width - 8;
                cancel.Left = ok.Left - cancel.Width - 8;
                ok.Top = cancel.Top = 10;
            };

            Controls.Add(_list);
            Controls.Add(top);
            Controls.Add(bottom);

            AcceptButton = ok;
            CancelButton = cancel;

            _filter.TextChanged += (s, e) => ApplyFilter();
            Load += (s, e) =>
            {
                try
                {
                    var names = HealthChecker.GetInstalledServiceNames();
                    _list.Items.AddRange(names);
                    _list.Sorted = true;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            _list.DoubleClick += (s, e) =>
            {
                if (_list.SelectedItem != null)
                {
                    SelectedService = _list.SelectedItem.ToString();
                    DialogResult = DialogResult.OK;
                    Close();
                }
            };

            ok.Click += (s, e) =>
            {
                if (_list.SelectedItem == null)
                {
                    MessageBox.Show(this, "Выберите службу из списка.", "Watch Dogs",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.None;
                    return;
                }
                SelectedService = _list.SelectedItem.ToString();
            };
        }

        private void ApplyFilter()
        {
            if (_fullList == null)
            {
                _fullList = new List<string>();
                foreach (var o in _list.Items)
                    _fullList.Add(o.ToString());
            }

            var f = _filter.Text.Trim();
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var n in _fullList)
            {
                if (f.Length == 0 || n.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0)
                    _list.Items.Add(n);
            }
            _list.EndUpdate();
        }
    }
}
