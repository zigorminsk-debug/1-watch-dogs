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

        // --- log ---
        private readonly TextBox _txtLog;

        // --- common ---
        private readonly Timer _refreshTimer;
        private readonly StatusStrip _statusStrip;
        private readonly ToolStripStatusLabel _statusLabel;

        public MainForm()
        {
            Text = "Watch Dogs — мониторинг службы 1С";
            Width = 900;
            Height = 680;
            MinimumSize = new Size(780, 560);
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9F);

            var tabs = new TabControl { Dock = DockStyle.Fill };

            // ===================== Tab 1: Status =====================
            var tabStatus = new TabPage("Состояние");
            var statusPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };

            var lblTitle = new Label
            {
                Text = "Служба мониторинга Watch Dogs",
                Font = new Font(Font, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(12, 12)
            };

            _lblMonitorService = MakeInfoLabel();
            _lblTargetService = MakeInfoLabel();
            _lblState = MakeInfoLabel();
            _lblHealthy = MakeInfoLabel();
            _lblMessage = MakeInfoLabel();
            _lblCounters = MakeInfoLabel();
            _lblLastCheck = MakeInfoLabel();

            var y = 44;
            y = AddRow(statusPanel, "Служба мониторинга:", _lblMonitorService, y);
            y = AddRow(statusPanel, "Служба 1С:", _lblTargetService, y);
            y = AddRow(statusPanel, "Состояние движка:", _lblState, y);
            y = AddRow(statusPanel, "Последняя проверка:", _lblHealthy, y);
            y = AddRow(statusPanel, "Сообщение:", _lblMessage, y, 56);
            y = AddRow(statusPanel, "Время проверки (UTC):", _lblLastCheck, y);
            y = AddRow(statusPanel, "Счётчики:", _lblCounters, y);

            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                Padding = new Padding(8),
                FlowDirection = FlowDirection.LeftToRight
            };

            var btnRefresh = new Button { Text = "Обновить", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            btnRefresh.Click += (s, e) => RefreshStatus();

            var btnStartMonitor = new Button { Text = "Запустить службу мониторинга", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            btnStartMonitor.Click += async (s, e) => await ControlMonitoringServiceAsync(true);

            var btnStopMonitor = new Button { Text = "Остановить службу мониторинга", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            btnStopMonitor.Click += async (s, e) => await ControlMonitoringServiceAsync(false);

            var btnInstallMonitor = new Button { Text = "Установить службу", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            btnInstallMonitor.Click += async (s, e) => await InstallMonitoringServiceAsync();

            var btnUninstallMonitor = new Button { Text = "Удалить службу", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            btnUninstallMonitor.Click += async (s, e) => await UninstallMonitoringServiceAsync();

            var btnOpenLogs = new Button { Text = "Открыть папку логов", AutoSize = true, Padding = new Padding(8, 2, 8, 2) };
            btnOpenLogs.Click += (s, e) => OpenLogFolder();

            btnPanel.Controls.Add(btnRefresh);
            btnPanel.Controls.Add(btnInstallMonitor);
            btnPanel.Controls.Add(btnStartMonitor);
            btnPanel.Controls.Add(btnStopMonitor);
            btnPanel.Controls.Add(btnUninstallMonitor);
            btnPanel.Controls.Add(btnOpenLogs);

            statusPanel.Controls.Add(lblTitle);
            statusPanel.Controls.Add(btnPanel);
            tabStatus.Controls.Add(statusPanel);

            // ===================== Tab 2: Settings =====================
            var tabSettings = new TabPage("Настройки");
            var settingsPanel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(12) };

            var lblHint = new Label
            {
                Text = "Все параметры применяются службе мониторинга после нажатия «Сохранить». " +
                       "Если служба запущена — перечитает конфигурацию автоматически при следующей проверке.",
                AutoSize = false,
                Height = 36,
                Width = 820,
                ForeColor = Color.DimGray,
                Location = new Point(12, 10)
            };

            var grpTarget = new GroupBox { Text = "Целевая служба 1С", Location = new Point(12, 52), Size = new Size(830, 118) };
            _cbTargetService = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Location = new Point(170, 26),
                Size = new Size(320, 24)
            };
            var btnPick = new Button { Text = "Из списка…", Location = new Point(500, 24), AutoSize = true };
            btnPick.Click += (s, e) => PickService();
            var lblTargetCaption = new Label { Text = "Имя службы (Service Name):", Location = new Point(16, 30), AutoSize = true };

            _cbCheckType = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(170, 58), Size = new Size(320, 24) };
            _cbCheckType.Items.Add("Статус службы (Running)");
            _cbCheckType.Items.Add("Статус + TCP-порт");
            _cbCheckType.Items.Add("Статус + HTTP-адрес");
            _cbCheckType.SelectedIndex = 0;
            var lblCheckType = new Label { Text = "Тип проверки «отвечает»:", Location = new Point(16, 62), AutoSize = true };

            var lblTcpHint = new Label
            {
                Text = "TCP/HTTP используются, когда нужно проверять не только факт запуска, но и ответ процесса 1С.",
                Location = new Point(16, 90),
                AutoSize = true,
                ForeColor = Color.DimGray
            };
            grpTarget.Controls.Add(lblTargetCaption);
            grpTarget.Controls.Add(_cbTargetService);
            grpTarget.Controls.Add(btnPick);
            grpTarget.Controls.Add(lblCheckType);
            grpTarget.Controls.Add(_cbCheckType);
            grpTarget.Controls.Add(lblTcpHint);

            var grpDeep = new GroupBox { Text = "Параметры глубокой проверки", Location = new Point(12, 180), Size = new Size(830, 86) };
            _txtTcpHost = new TextBox { Location = new Point(170, 24), Size = new Size(160, 24), Text = "localhost" };
            _numTcpPort = new NumericUpDown { Location = new Point(430, 24), Size = new Size(90, 24), Minimum = 1, Maximum = 65535, Value = 1541 };
            _txtHttpUrl = new TextBox { Location = new Point(170, 54), Size = new Size(400, 24), Text = "http://localhost/health" };
            grpDeep.Controls.Add(new Label { Text = "TCP хост:", Location = new Point(16, 27), AutoSize = true });
            grpDeep.Controls.Add(_txtTcpHost);
            grpDeep.Controls.Add(new Label { Text = "порт:", Location = new Point(360, 27), AutoSize = true });
            grpDeep.Controls.Add(_numTcpPort);
            grpDeep.Controls.Add(new Label { Text = "HTTP URL:", Location = new Point(16, 57), AutoSize = true });
            grpDeep.Controls.Add(_txtHttpUrl);

            var grpTiming = new GroupBox { Text = "Тайминги и перезапуск", Location = new Point(12, 278), Size = new Size(830, 170) };
            _numInterval = MakeNum(5, 86400, 30);
            _numBootDelay = MakeNum(0, 1440, 5);
            _numTimeout = MakeNum(1, 300, 10);
            _numMaxRestarts = MakeNum(1, 100, 5);
            _numCooldown = MakeNum(0, 1440, 5);
            _chkAutoRestart = new CheckBox { Text = "Автоматически перезапускать службу 1С при отказе", Location = new Point(16, 24), AutoSize = true, Checked = true };
            _chkEnabled = new CheckBox { Text = "Мониторинг включён", Location = new Point(16, 50), AutoSize = true, Checked = true };

            PlaceLabeled(grpTiming, "Интервал проверки (сек):", _numInterval, 80);
            PlaceLabeled(grpTiming, "Задержка после загрузки (мин):", _numBootDelay, 110);
            PlaceLabeled(grpTiming, "Таймаут ответа (сек):", _numTimeout, 140);
            // second column
            _numMaxRestarts.Location = new Point(520, 80);
            _numCooldown.Location = new Point(520, 110);
            grpTiming.Controls.Add(new Label { Text = "Макс. перезапусков подряд:", Location = new Point(340, 83), AutoSize = true });
            grpTiming.Controls.Add(_numMaxRestarts);
            grpTiming.Controls.Add(new Label { Text = "Пауза между перезапусками (мин):", Location = new Point(340, 113), AutoSize = true });
            grpTiming.Controls.Add(_numCooldown);
            grpTiming.Controls.Add(_chkAutoRestart);
            grpTiming.Controls.Add(_chkEnabled);

            var btnPanelSettings = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(8),
                FlowDirection = FlowDirection.LeftToRight
            };

            _btnSave = new Button { Text = "Сохранить настройки", AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
            _btnSave.Click += async (s, e) => await SaveSettingsAsync();

            var btnReload = new Button { Text = "Загрузить текущие", AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
            btnReload.Click += (s, e) => LoadSettings();

            var btnTest = new Button { Text = "Проверить сейчас", AutoSize = true, Padding = new Padding(10, 3, 10, 3) };
            btnTest.Click += async (s, e) => await TestCheckAsync();

            btnPanelSettings.Controls.Add(_btnSave);
            btnPanelSettings.Controls.Add(btnReload);
            btnPanelSettings.Controls.Add(btnTest);

            settingsPanel.Controls.Add(lblHint);
            settingsPanel.Controls.Add(grpTarget);
            settingsPanel.Controls.Add(grpDeep);
            settingsPanel.Controls.Add(grpTiming);
            tabSettings.Controls.Add(settingsPanel);
            settingsPanel.Controls.Add(btnPanelSettings);

            // ===================== Tab 3: Logs =====================
            var tabLog = new TabPage("Журнал");
            var logPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

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
            var btnRefreshLog = new Button { Text = "Обновить журнал", AutoSize = true };
            btnRefreshLog.Click += (s, e) => LoadLog();
            var btnOpenLogDir = new Button { Text = "Открыть папку", AutoSize = true };
            btnOpenLogDir.Click += (s, e) => OpenLogFolder();
            logButtons.Controls.Add(btnRefreshLog);
            logButtons.Controls.Add(btnOpenLogDir);

            logPanel.Controls.Add(_txtLog);
            logPanel.Controls.Add(logButtons);
            tabLog.Controls.Add(logPanel);

            // ===================== Assemble =====================
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

        // ---------- helpers ----------

        private static Label MakeInfoLabel()
        {
            return new Label
            {
                AutoSize = false,
                Width = 620,
                Height = 20,
                Text = "—",
                ForeColor = Color.FromArgb(30, 30, 30)
            };
        }

        private static int AddRow(Panel parent, string caption, Label value, int y, int valueHeight = 20)
        {
            var lbl = new Label
            {
                Text = caption,
                Location = new Point(12, y + 2),
                AutoSize = true
            };
            lbl.Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold);

            value.Location = new Point(230, y);
            value.Height = valueHeight;
            value.Width = 620;

            parent.Controls.Add(lbl);
            parent.Controls.Add(value);
            return y + valueHeight + 10;
        }

        private static NumericUpDown MakeNum(decimal min, decimal max, decimal val)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = val,
                Size = new Size(90, 24)
            };
        }

        private static void PlaceLabeled(GroupBox grp, string caption, Control ctl, int y)
        {
            var lbl = new Label { Text = caption, Location = new Point(16, y + 3), AutoSize = true };
            ctl.Location = new Point(260, y);
            grp.Controls.Add(lbl);
            grp.Controls.Add(ctl);
        }

        private void SetStatus(string text)
        {
            if (_statusLabel != null)
                _statusLabel.Text = text;
        }

        // ---------- status tab ----------

        private void RefreshStatus()
        {
            try
            {
                _lblMonitorService.Text = ServiceInstallerHelper.GetStatusText();

                var st = ConfigStore.LoadStatus();
                if (st == null)
                {
                    _lblTargetService.Text = "—";
                    _lblState.Text = "нет данных (служба мониторинга ещё не писала status.json)";
                    _lblHealthy.Text = "—";
                    _lblMessage.Text = "—";
                    _lblLastCheck.Text = "—";
                    _lblCounters.Text = "—";
                    return;
                }

                _lblTargetService.Text =
                    (st.TargetServiceName ?? "—") + "  →  " + (st.TargetServiceStatus ?? "—");
                _lblState.Text = st.State ?? "—";
                _lblHealthy.Text = st.LastCheckHealthy ? "OK — отвечает" : "FAIL — не отвечает";
                _lblHealthy.ForeColor = st.LastCheckHealthy ? Color.Green : Color.Firebrick;
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

        // ---------- settings tab ----------

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
                _cbCheckType.SelectedIndex = (int)cfg.CheckType;
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

                MessageBox.Show(this,
                    "Настройки сохранены.\n\nСлужба мониторинга перечитает конфигурацию автоматически " +
                    "при следующей проверке (не позднее " + cfg.CheckIntervalSeconds + " сек).\n\n" +
                    "Файл: " + Paths.ConfigPath,
                    "Watch Dogs", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                var text = (result.IsHealthy ? "OK: " : "FAIL: ") + result.Message +
                           "\nСостояние службы: " + result.ServiceStatus;
                MessageBox.Show(this, text, "Проверка сейчас",
                    MessageBoxButtons.OK, result.IsHealthy ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
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

        private void PickService()
        {
            using (var dlg = new ServicePickerForm())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrEmpty(dlg.SelectedService))
                    _cbTargetService.Text = dlg.SelectedService;
            }
        }

        // ---------- logs tab ----------

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

        public string SelectedService { get; private set; }

        public ServicePickerForm()
        {
            Text = "Выбор службы 1С";
            Width = 480;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            Font = new Font("Segoe UI", 9F);

            _filter = new TextBox { Dock = DockStyle.Top, Height = 28 };
            _list = new ListBox
            {
                Dock = DockStyle.Fill,
                DisplayMember = null
            };

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 44 };
            var ok = new Button { Text = "Выбрать", DialogResult = DialogResult.OK, Dock = DockStyle.Right, Width = 100 };
            var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Dock = DockStyle.Right, Width = 100 };
            bottom.Controls.Add(ok);
            bottom.Controls.Add(cancel);

            Controls.Add(_list);
            Controls.Add(_filter);
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
            // Rebuild list based on filter — simple approach: reload is heavy,
            // so filter by re-querying stored full list once.
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

        private System.Collections.Generic.List<string> _fullList;
    }
}
