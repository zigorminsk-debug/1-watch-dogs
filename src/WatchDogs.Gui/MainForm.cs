using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using WatchDogs.Core;

namespace WatchDogs.Gui
{
    public sealed class MainForm : Form
    {
        // --- status controls ---
        private readonly Label _lblMonitorService;
        private readonly Label _lblTargetService;
        private readonly Label _lblState;
        private readonly Label _lblHealthy;
        private readonly Label _lblMessage;
        private readonly Label _lblCounters;
        private readonly Label _lblLastCheck;
        private readonly Label _lblMonitorHint;

        // --- settings controls ---
        private readonly ComboBox _cbTargetService;
        private readonly ComboBox _cbCheckType;
        private readonly TextBox _txtTcpHost;
        private readonly NumericUpDown _numTcpPort;
        private readonly TextBox _txtHttpUrl;
        private readonly NumericUpDown _numInterval;
        private readonly NumericUpDown _numBootDelay;
        private readonly NumericUpDown _numTimeout;
        private readonly CheckBox _chkAutoRestart;
        private readonly NumericUpDown _numMaxRestarts;
        private readonly NumericUpDown _numCooldown;
        private readonly CheckBox _chkEnabled;
        private readonly Button _btnSave;
        private readonly Button _btnRestart1C;

        // --- log ---
        private readonly TextBox _txtLog;

        // --- common ---
        private readonly Timer _refreshTimer;
        private readonly StatusStrip _statusStrip;
        private readonly ToolStripStatusLabel _statusLabel;

        public MainForm()
        {
            Text = "Watch Dogs — мониторинг службы 1С";
            Width = 960;
            Height = 720;
            MinimumSize = new Size(860, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;

            var tabs = new TabControl { Dock = DockStyle.Fill };

            // ===================== Tab 1: Status =====================
            var tabStatus = new TabPage("Состояние");
            BuildStatusTab(tabStatus);

            // ===================== Tab 2: Settings =====================
            var tabSettings = new TabPage("Настройки");
            BuildSettingsTab(tabSettings);

            // ===================== Tab 3: Logs =====================
            var tabLog = new TabPage("Журнал");
            BuildLogTab(tabLog);

            tabs.TabPages.Add(tabStatus);
            tabs.TabPages.Add(tabSettings);
            tabs.TabPages.Add(tabLog);

            _statusStrip = new StatusStrip();
            _statusLabel = new ToolStripStatusLabel("Готово");
            _statusStrip.Items.Add(_statusLabel);

            Controls.Add(tabs);
            Controls.Add(_statusStrip);

            _refreshTimer = new Timer { Interval = 3000 };
            _refreshTimer.Tick += (s, e) => RefreshStatus();
            _refreshTimer.Start();

            Load += (s, e) =>
            {
                LoadSettings();
                RefreshStatus();
                LoadLog();
                LoadServiceList();
            };

            FormClosed += (s, e) => _refreshTimer.Stop();
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
                RowCount = 10
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var title = new Label
            {
                Text = "Служба мониторинга Watch Dogs",
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(title, 0, 0);
            root.SetColumnSpan(title, 2);

            _lblMonitorHint = new Label
            {
                AutoSize = false,
                Height = 38,
                Text = "",
                ForeColor = Color.DarkRed,
                Font = new Font(Font, FontStyle.Bold),
                Margin = new Padding(0, 0, 0, 8)
            };
            root.Controls.Add(_lblMonitorHint, 0, 1);
            root.SetColumnSpan(_lblMonitorHint, 2);

            _lblMonitorService = MakeInfoLabel();
            _lblTargetService = MakeInfoLabel();
            _lblState = MakeInfoLabel();
            _lblHealthy = MakeInfoLabel();
            _lblMessage = MakeInfoLabel { Height = 42 };
            _lblLastCheck = MakeInfoLabel();
            _lblCounters = MakeInfoLabel();

            var row = 2;
            AddStatusRow(root, ref row, "Служба мониторинга:", _lblMonitorService);
            AddStatusRow(root, ref row, "Служба 1С:", _lblTargetService);
            AddStatusRow(root, ref row, "Состояние движка:", _lblState);
            AddStatusRow(root, ref row, "Последняя проверка:", _lblHealthy);
            AddStatusRow(root, ref row, "Сообщение:", _lblMessage);
            AddStatusRow(root, ref row, "Время проверки (UTC):", _lblLastCheck);
            AddStatusRow(root, ref row, "Счётчики:", _lblCounters);

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 46,
                Padding = new Padding(8, 6, 8, 4),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true
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

            AddBtn("Обновить", (s, e) => RefreshStatus());
            AddBtn("Установить службу", async (s, e) => await InstallMonitoringServiceAsync());
            AddBtn("Запустить мониторинг", async (s, e) => await ControlMonitoringServiceAsync(true));
            AddBtn("Остановить мониторинг", async (s, e) => await ControlMonitoringServiceAsync(false));
            AddBtn("Удалить службу", async (s, e) => await UninstallMonitoringServiceAsync());
            AddBtn("Открыть логи", (s, e) => OpenLogFolder());

            tab.Controls.Add(root);
            tab.Controls.Add(btnPanel);
        }

        private static void AddStatusRow(TableLayoutPanel root, ref int row, string caption, Label value)
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
            row++;
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
        // Settings tab — TableLayoutPanel, no overlapping labels
        // =====================================================================

        private void BuildSettingsTab(TabPage tab)
        {
            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12, 8, 12, 0),
                ColumnCount = 1,
                RowCount = 5
            };
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var hint = new Label
            {
                Text = "После «Сохранить» служба мониторинга перечитает конфигурацию при следующей проверке " +
                       "(не позднее интервала). Если служба мониторинга не запущена — автоперезапуск 1С не работает.",
                AutoSize = true,
                MaximumSize = new Size(900, 0),
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 0, 0, 10)
            };
            outer.Controls.Add(hint, 0, 0);

            // ----- Group: target service -----
            var grpTarget = MakeGroup("Целевая служба 1С");
            var tlpTarget = MakeGroupTable(grpTarget);

            _cbTargetService = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3)
            };
            var btnPick = new Button
            {
                Text = "Из списка…",
                AutoSize = true,
                Padding = new Padding(10, 2, 10, 2),
                Margin = new Padding(8, 2, 0, 2)
            };
            btnPick.Click += (s, e) => PickService();

            _cbCheckType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3)
            };
            _cbCheckType.Items.AddRange(new object[]
            {
                "Статус службы (Running)",
                "Статус + TCP-порт",
                "Статус + HTTP-адрес"
            });
            _cbCheckType.SelectedIndex = 0;

            AddLabeledRow(tlpTarget, 0, "Имя службы (Service Name):", _cbTargetService, btnPick);
            AddLabeledRow(tlpTarget, 1, "Тип проверки «отвечает»:", _cbCheckType);

            var tcpHint = new Label
            {
                Text = "TCP/HTTP — когда нужно проверять не только факт запуска, но и ответ процесса 1С.",
                AutoSize = true,
                ForeColor = Color.DimGray,
                Margin = new Padding(0, 4, 0, 0)
            };
            tlpTarget.Controls.Add(tcpHint, 1, 2);
            outer.Controls.Add(grpTarget, 0, 1);

            // ----- Group: deep check -----
            var grpDeep = MakeGroup("Параметры глубокой проверки");
            var tlpDeep = MakeGroupTable(grpDeep);

            _txtTcpHost = new TextBox { Dock = DockStyle.Fill, Text = "localhost", Margin = new Padding(0, 3, 0, 3) };
            _numTcpPort = MakeNum(1, 65535, 1541);
            _numTcpPort.Dock = DockStyle.Left;
            _numTcpPort.Width = 100;
            _txtHttpUrl = new TextBox { Dock = DockStyle.Fill, Text = "http://localhost/health", Margin = new Padding(0, 3, 0, 3) };

            var hostPanel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Height = 30 };
            hostPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            hostPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            hostPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            hostPanel.Controls.Add(_txtTcpHost, 0, 0);
            var lblPort = new Label { Text = "порт:", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 6, 4, 0) };
            hostPanel.Controls.Add(lblPort, 1, 0);
            hostPanel.Controls.Add(_numTcpPort, 2, 0);

            AddLabeledRow(tlpDeep, 0, "TCP хост:", hostPanel);
            AddLabeledRow(tlpDeep, 1, "HTTP URL:", _txtHttpUrl);
            outer.Controls.Add(grpDeep, 0, 2);

            // ----- Group: timing / restart -----
            var grpTiming = MakeGroup("Тайминги, перезапуск и включение");
            var tlpTiming = MakeGroupTable(grpTiming);

            _chkAutoRestart = new CheckBox
            {
                Text = "Автоматически перезапускать службу 1С при отказе",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 2, 0, 2)
            };
            _chkEnabled = new CheckBox
            {
                Text = "Мониторинг включён",
                AutoSize = true,
                Checked = true,
                Margin = new Padding(0, 2, 0, 10)
            };
            tlpTiming.Controls.Add(_chkAutoRestart, 1, 0);
            tlpTiming.Controls.Add(_chkEnabled, 1, 1);
            tlpTiming.Controls.Add(new Label { AutoSize = true }, 0, 0);
            tlpTiming.Controls.Add(new Label { AutoSize = true }, 0, 1);

            _numInterval = MakeNum(5, 86400, 30);
            _numBootDelay = MakeNum(0, 1440, 5);
            _numTimeout = MakeNum(1, 300, 10);
            _numMaxRestarts = MakeNum(1, 100, 5);
            _numCooldown = MakeNum(0, 1440, 5);

            AddLabeledRow(tlpTiming, 2, "Интервал проверки (сек):", _numInterval);
            AddLabeledRow(tlpTiming, 3, "Задержка после загрузки (мин):", _numBootDelay);
            AddLabeledRow(tlpTiming, 4, "Таймаут ответа (сек):", _numTimeout);
            AddLabeledRow(tlpTiming, 5, "Макс. перезапусков подряд:", _numMaxRestarts);
            AddLabeledRow(tlpTiming, 6, "Пауза между перезапусками (мин):", _numCooldown);
            outer.Controls.Add(grpTiming, 0, 3);

            // ----- Bottom buttons -----
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(0, 10, 0, 6),
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnSave = MakeActionButton("Сохранить настройки", async (s, e) => await SaveSettingsAsync(), true);
            var btnReload = MakeActionButton("Загрузить текущие", (s, e) => LoadSettings(), false);
            var btnTest = MakeActionButton("Проверить сейчас", async (s, e) => await TestCheckAsync(), false);
            _btnRestart1C = MakeActionButton("Перезапустить 1С", async (s, e) => await RestartFromSettingsAsync(), false);

            btnPanel.Controls.Add(_btnSave);
            btnPanel.Controls.Add(btnReload);
            btnPanel.Controls.Add(btnTest);
            btnPanel.Controls.Add(_btnRestart1C);

            outer.Controls.Add(btnPanel, 0, 4);
            tab.Controls.Add(outer);
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

        private static TableLayoutPanel MakeGroupTable(GroupBox grp)
        {
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 1
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            grp.Controls.Add(tlp);
            return tlp;
        }

        private static void AddLabeledRow(TableLayoutPanel tlp, int row, string caption, params Control[] controls)
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

            tlp.Controls.Add(lbl, 0, row);

            if (controls.Length == 1)
            {
                var c = controls[0];
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                if (c.Margin == Padding.Empty || c.Margin.Left == 0 && c.Margin.Top == 0)
                    c.Margin = new Padding(0, 4, 0, 4);
                tlp.Controls.Add(c, 1, row);
            }
            else
            {
                var panel = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    AutoSize = true,
                    WrapContents = false,
                    Margin = new Padding(0, 2, 0, 2)
                };
                foreach (var c in controls)
                {
                    c.Margin = new Padding(0, 2, 8, 2);
                    panel.Controls.Add(c);
                }
                tlp.Controls.Add(panel, 1, row);
            }
        }

        private static NumericUpDown MakeNum(decimal min, decimal max, decimal val)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = val,
                Width = 110,
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
            {
                b.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);
            }
            b.Click += onClick;
            return b;
        }

        // =====================================================================
        // Log tab
        // =====================================================================

        private void BuildLogTab(TabPage tab)
        {
            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

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
                        "⚠ Служба мониторинга НЕ установлена — автоперезапуск 1С не работает. " +
                        "Нажмите «Установить службу» на этой вкладке или в настройках после сохранения.";
                }
                else
                {
                    _lblMonitorHint.Text =
                        "⚠ Служба мониторинга установлена, но не запущена (статус: " + monitorStatus + "). " +
                        "Нажмите «Запустить мониторинг» — иначе автоперезапуск 1С не работает.";
                }

                var st = ConfigStore.LoadStatus();
                if (st == null)
                {
                    _lblTargetService.Text = "—";
                    _lblState.Text = "нет данных (служба мониторинга ещё не писала status.json)";
                    _lblHealthy.Text = "—";
                    _lblHealthy.ForeColor = Color.Gray;
                    _lblMessage.Text = "—";
                    _lblLastCheck.Text = "—";
                    _lblCounters.Text = "—";
                    return;
                }

                _lblTargetService.Text =
                    (st.TargetServiceName ?? "—") + "  →  " + (st.TargetServiceStatus ?? "—");
                _lblState.Text = st.State ?? "—";

                if (st.State == "WaitingBootDelay")
                {
                    _lblHealthy.Text = "ожидание задержки после загрузки…";
                    _lblHealthy.ForeColor = Color.DarkOrange;
                }
                else
                {
                    _lblHealthy.Text = st.LastCheckHealthy ? "OK — отвечает" : "FAIL — не отвечает";
                    _lblHealthy.ForeColor = st.LastCheckHealthy ? Color.Green : Color.Firebrick;
                }

                _lblMessage.Text = st.LastCheckMessage ?? "—";
                _lblLastCheck.Text = st.LastCheckTimeUtc ?? "—";
                _lblCounters.Text = string.Format(
                    "проверок: {0}, перезапусков всего: {1}, подряд: {2}, последний: {3}",
                    st.TotalChecks, st.TotalRestarts, st.ConsecutiveRestarts,
                    st.LastRestartTimeUtc ?? "—");
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
                "Сама программа и настройки останутся на диске. " +
                "Полное удаление выполняется через «Установка и удаление программ» Windows.",
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
        // Settings logic
        // =====================================================================

        private void LoadServiceList()
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
                // combo stays editable even if enumeration fails
            }
        }

        private void LoadSettings()
        {
            try
            {
                var cfg = ConfigStore.LoadConfig();
                _cbTargetService.Text = cfg.TargetServiceName ?? "";
                _cbCheckType.SelectedIndex = Math.Max(0, Math.Min(2, (int)cfg.CheckType));
                _txtTcpHost.Text = cfg.TcpHost;
                _numTcpPort.Value = Clamp(cfg.TcpPort, 1, 65535);
                _txtHttpUrl.Text = cfg.HttpUrl;
                _numInterval.Value = Clamp(cfg.CheckIntervalSeconds, 5, 86400);
                _numBootDelay.Value = Clamp(cfg.BootDelayMinutes, 0, 1440);
                _numTimeout.Value = Clamp(cfg.ResponseTimeoutSeconds, 1, 300);
                _chkAutoRestart.Checked = cfg.AutoRestart;
                _numMaxRestarts.Value = Clamp(cfg.MaxRestartAttempts, 1, 100);
                _numCooldown.Value = Clamp(cfg.RestartCooldownMinutes, 0, 1440);
                _chkEnabled.Checked = cfg.MonitoringEnabled;
                SetStatus("Настройки загружены");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Не удалось загрузить настройки:\n" + ex.Message,
                    "Watch Dogs", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private AppConfig CollectSettings()
        {
            var cfg = AppConfig.CreateDefault();
            cfg.TargetServiceName = _cbTargetService.Text;
            cfg.CheckType = (HealthCheckType)Math.Max(0, Math.Min(2, _cbCheckType.SelectedIndex));
            cfg.TcpHost = _txtTcpHost.Text;
            cfg.TcpPort = (int)_numTcpPort.Value;
            cfg.HttpUrl = _txtHttpUrl.Text;
            cfg.CheckIntervalSeconds = (int)_numInterval.Value;
            cfg.BootDelayMinutes = (int)_numBootDelay.Value;
            cfg.ResponseTimeoutSeconds = (int)_numTimeout.Value;
            cfg.AutoRestart = _chkAutoRestart.Checked;
            cfg.MaxRestartAttempts = (int)_numMaxRestarts.Value;
            cfg.RestartCooldownMinutes = (int)_numCooldown.Value;
            cfg.MonitoringEnabled = _chkEnabled.Checked;
            cfg.Normalize();
            return cfg;
        }

        private async Task SaveSettingsAsync()
        {
            try
            {
                var cfg = CollectSettings();
                if (string.IsNullOrWhiteSpace(cfg.TargetServiceName))
                {
                    MessageBox.Show(this, "Укажите имя службы 1С.", "Watch Dogs",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await Task.Run(() => ConfigStore.SaveConfig(cfg));
                SetStatus("Настройки сохранены в " + Paths.ConfigPath);
                Log.Info("Настройки сохранены GUI. Служба 1С='" + cfg.TargetServiceName + "'");

                // Ensure monitoring service is running so auto-restart actually works
                var monitorStatus = ServiceInstallerHelper.GetStatusText();
                if (monitorStatus == "NOT_INSTALLED")
                {
                    var r = MessageBox.Show(this,
                        "Настройки сохранены.\n\n" +
                        "Служба мониторинга WatchDogsMonitor ещё НЕ установлена — " +
                        "автоперезапуск 1С работать не будет.\n\n" +
                        "Установить и запустить её сейчас?",
                        "Watch Dogs",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
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
                        "Настройки сохранены.\n\n" +
                        "Служба мониторинга сейчас: " + monitorStatus + ".\n" +
                        "Запустить её, чтобы автоперезапуск 1С заработал?",
                        "Watch Dogs",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (r == DialogResult.Yes)
                    {
                        await ControlMonitoringServiceAsync(true);
                        RefreshStatus();
                        return;
                    }
                }

                MessageBox.Show(this,
                    "Настройки сохранены.\n\n" +
                    "Служба мониторинга перечитает конфигурацию при следующей проверке " +
                    "(не позднее " + cfg.CheckIntervalSeconds + " сек).\n\n" +
                    "Файл: " + Paths.ConfigPath,
                    "Watch Dogs", MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка сохранения", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task TestCheckAsync()
        {
            try
            {
                var cfg = CollectSettings();
                _btnSave.Enabled = false;
                var result = await Task.Run(() => HealthChecker.Check(cfg));

                if (result.IsHealthy)
                {
                    MessageBox.Show(this,
                        "OK: " + result.Message + "\nСостояние службы: " + result.ServiceStatus,
                        "Проверка сейчас",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var answer = MessageBox.Show(this,
                    "FAIL: " + result.Message +
                    "\nСостояние службы: " + result.ServiceStatus +
                    "\n\nПерезапустить службу 1С сейчас?",
                    "Проверка сейчас",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (answer == DialogResult.Yes)
                    await RestartTargetServiceAsync(cfg);
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

        private async Task RestartTargetServiceAsync(AppConfig cfg)
        {
            try
            {
                SetStatus("Перезапуск службы 1С '" + cfg.TargetServiceName + "'…");
                var outcome = await Task.Run(() =>
                {
                    string msg;
                    var ok = HealthChecker.TryRestart(cfg, out msg);
                    return new { ok, msg };
                });

                SetStatus(outcome.msg);
                var icon = outcome.ok ? MessageBoxIcon.Information : MessageBoxIcon.Error;
                var title = outcome.ok ? "Перезапуск выполнен" : "Ошибка перезапуска";
                MessageBox.Show(this, outcome.msg, title, MessageBoxButtons.OK, icon);
                RefreshStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка перезапуска", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task RestartFromSettingsAsync()
        {
            var cfg = CollectSettings();
            if (string.IsNullOrWhiteSpace(cfg.TargetServiceName))
            {
                MessageBox.Show(this, "Укажите имя службы 1С.", "Watch Dogs",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var confirm = MessageBox.Show(this,
                "Перезапустить службу 1С\n\"" + cfg.TargetServiceName + "\"?\n\n" +
                "Сейчас она: " + HealthChecker.GetServiceStatusText(cfg.TargetServiceName),
                "Перезапуск службы 1С",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes)
                return;

            await RestartTargetServiceAsync(cfg);
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

    /// <summary>Simple modal dialog to pick an installed Windows service.</summary>
    internal sealed class ServicePickerForm : Form
    {
        private readonly ListBox _list;
        private readonly TextBox _filter;
        private System.Collections.Generic.List<string> _fullList;

        public string SelectedService { get; private set; }

        public ServicePickerForm()
        {
            Text = "Выбор службы 1С";
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
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(520 - 130, 10)
            };
            var cancel = new Button
            {
                Text = "Отмена",
                DialogResult = DialogResult.Cancel,
                Width = 110,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(520 - 250, 10)
            };
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);
            bottom.Resize += (s, e) =>
            {
                ok.Left = bottom.ClientSize.Width - ok.Width - 8;
                cancel.Left = ok.Left - cancel.Width - 8;
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
                _fullList = new System.Collections.Generic.List<string>();
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
