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
    /// <summary>Unified visual theme: one palette, one button style, one spacing.</summary>
    internal static class Ui
    {
        public static readonly Color Bg = Color.FromArgb(245, 247, 250);
        public static readonly Color Surface = Color.White;
        public static readonly Color Border = Color.FromArgb(220, 226, 234);
        public static readonly Color Text = Color.FromArgb(32, 40, 48);
        public static readonly Color Muted = Color.FromArgb(100, 116, 139);
        public static readonly Color Primary = Color.FromArgb(13, 71, 161);
        public static readonly Color PrimaryHover = Color.FromArgb(21, 101, 192);
        public static readonly Color Danger = Color.FromArgb(180, 35, 35);
        public static readonly Color Ok = Color.FromArgb(22, 120, 60);
        public static readonly Color WarnBg = Color.FromArgb(255, 244, 229);

        public static readonly Font TitleFont = new Font("Segoe UI", 11F, FontStyle.Bold);
        public static readonly Font SectionFont = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        public static readonly Font BodyFont = new Font("Segoe UI", 9F);
        public static readonly Font MonoFont = new Font("Consolas", 9F);

        public const int BtnHeight = 34;
        public const int InputHeight = 30;

        /// <summary>Primary or secondary button — always the same height and padding.</summary>
        public static Button Btn(string text, EventHandler onClick, bool primary = false, bool danger = false)
        {
            var b = new Button
            {
                Text = text,
                Height = BtnHeight,
                AutoSize = false,
                Width = Math.Max(120, TextRenderer.MeasureText(text, BodyFont).Width + 32),
                FlatStyle = FlatStyle.Flat,
                Font = BodyFont,
                Margin = new Padding(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            if (danger)
            {
                b.BackColor = Color.FromArgb(252, 238, 238);
                b.ForeColor = Danger;
                b.FlatAppearance.BorderColor = Color.FromArgb(230, 180, 180);
            }
            else if (primary)
            {
                b.BackColor = Primary;
                b.ForeColor = Color.White;
                b.FlatAppearance.BorderColor = Primary;
                b.Font = SectionFont;
            }
            else
            {
                b.BackColor = Surface;
                b.ForeColor = Text;
                b.FlatAppearance.BorderColor = Border;
            }
            b.FlatAppearance.MouseOverBackColor = danger
                ? Color.FromArgb(255, 230, 230)
                : (primary ? PrimaryHover : Color.FromArgb(238, 242, 247));
            b.FlatAppearance.MouseDownBackColor = danger
                ? Color.FromArgb(250, 215, 215)
                : (primary ? Color.FromArgb(10, 55, 130) : Color.FromArgb(230, 236, 244));
            if (onClick != null)
                b.Click += onClick;
            return b;
        }

        public static NumericUpDown Num(decimal min, decimal max, decimal val)
        {
            return new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = val,
                Height = InputHeight,
                Width = 120,
                Font = BodyFont,
                Margin = new Padding(0, 4, 0, 4)
            };
        }

        public static Label Section(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Font = SectionFont,
                ForeColor = Text,
                Margin = new Padding(0, 0, 0, 6)
            };
        }

        public static Label Hint(string text)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                MaximumSize = new Size(980, 0),
                Font = BodyFont,
                ForeColor = Muted,
                Margin = new Padding(0, 0, 0, 8)
            };
        }

        public static Panel Card(Control content, DockStyle dock = DockStyle.Fill)
        {
            var p = new Panel
            {
                Dock = dock,
                BackColor = Surface,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 8)
            };
            p.Controls.Add(content);
            return p;
        }

        public static FlowLayoutPanel Toolbar()
        {
            return new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 46,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(0, 6, 0, 0),
                BackColor = Bg
            };
        }

        public static DataGridView Grid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = Surface,
                BorderStyle = BorderStyle.FixedSingle,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EnableHeadersVisualStyles = false,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(238, 242, 247),
                    ForeColor = Text,
                    Font = SectionFont,
                    Alignment = DataGridViewContentAlignment.MiddleLeft,
                    Padding = new Padding(6, 4, 6, 4)
                },
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                GridColor = Color.FromArgb(236, 240, 245),
                RowTemplate = { Height = 28 },
                Font = BodyFont,
                Margin = new Padding(0)
            };
            g.DefaultCellStyle = new DataGridViewCellStyle
            {
                BackColor = Surface,
                ForeColor = Text,
                SelectionBackColor = Color.FromArgb(220, 232, 248),
                SelectionForeColor = Text,
                Padding = new Padding(6, 2, 6, 2)
            };
            return g;
        }

        public static void StyleTab(TabPage tab)
        {
            tab.BackColor = Bg;
            tab.Padding = new Padding(8);
        }
    }

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

        private AppConfig _cfg;

        public MainForm()
        {
            Text = "Watch Dogs — мониторинг служб 1С";
            Width = 1080;
            Height = 760;
            MinimumSize = new Size(960, 640);
            StartPosition = FormStartPosition.CenterScreen;
            Font = Ui.BodyFont;
            BackColor = Ui.Bg;
            AutoScaleMode = AutoScaleMode.Dpi;

            ApplyBackgroundIcon();

            var tabs = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(14, 8),
                Font = Ui.BodyFont,
                BackColor = Ui.Bg
            };

            var tabStatus = new TabPage("Состояние");
            BuildStatusTab(tabStatus);

            var tabSettings = new TabPage("Настройки");
            BuildSettingsTab(tabSettings);

            var tabLog = new TabPage("Журнал");
            BuildLogTab(tabLog);

            tabs.TabPages.Add(tabStatus);
            tabs.TabPages.Add(tabSettings);
            tabs.TabPages.Add(tabLog);

            _statusStrip = new StatusStrip
            {
                BackColor = Color.FromArgb(238, 242, 247),
                SizingGrip = false
            };
            _statusLabel = new ToolStripStatusLabel("Готово") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            var devLabel = new ToolStripStatusLabel("csl.by · +375 29 337-14-12")
            {
                IsLink = true,
                LinkBehavior = LinkBehavior.HoverUnderline,
                ForeColor = Ui.Primary
            };
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
        // Soft icon watermark (cosmetic)
        // =====================================================================

        private void ApplyBackgroundIcon()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var name = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("watchdogs-icon.png", StringComparison.OrdinalIgnoreCase));
                if (name == null)
                    return;
                using (var s = asm.GetManifestResourceStream(name))
                {
                    if (s == null)
                        return;
                    using (var icon = new Bitmap(s))
                    using (var tile = new Bitmap(480, 360, PixelFormat.Format32bppArgb))
                    {
                        using (var g = Graphics.FromImage(tile))
                        {
                            g.Clear(Color.FromArgb(245, 247, 250));
                            var size = 200;
                            var cm = new ColorMatrix { Matrix33 = 0.06f };
                            using (var ia = new ImageAttributes())
                            {
                                ia.SetColorMatrix(cm);
                                g.DrawImage(icon,
                                    new Rectangle(tile.Width - size - 24, tile.Height - size - 24, size, size),
                                    0, 0, icon.Width, icon.Height, GraphicsUnit.Pixel, ia);
                            }
                        }
                        BackgroundImage = tile;
                        BackgroundImageLayout = ImageLayout.Tile;
                    }
                }
            }
            catch
            {
                // cosmetic only
            }
        }

        // =====================================================================
        // Status tab
        // =====================================================================

        private void BuildStatusTab(TabPage tab)
        {
            Ui.StyleTab(tab);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(4),
                BackColor = Ui.Bg
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label
            {
                Text = "Состояние мониторинга",
                Font = Ui.TitleFont,
                ForeColor = Ui.Text,
                AutoSize = true,
                Margin = new Padding(4, 4, 0, 10)
            };
            root.Controls.Add(title, 0, 0);
            root.SetColumnSpan(title, 2);

            var statusCard = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                RowCount = 3,
                BackColor = Ui.Surface,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 0, 0, 10)
            };
            statusCard.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
            statusCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            _lblMonitorHint = new Label
            {
                AutoSize = false,
                Height = 34,
                Text = "",
                Font = Ui.BodyFont,
                ForeColor = Ui.Danger,
                Margin = new Padding(0, 0, 0, 4)
            };

            _lblMonitorService = MakeInfoLabel();
            _lblEngineState = MakeInfoLabel();

            AddStatusRow(statusCard, 0, "Служба мониторинга:", _lblMonitorService);
            AddStatusRow(statusCard, 1, "Движок:", _lblEngineState);
            statusCard.Controls.Add(_lblMonitorHint, 0, 2);
            statusCard.SetColumnSpan(_lblMonitorHint, 2);

            root.Controls.Add(statusCard, 0, 1);
            root.SetColumnSpan(statusCard, 2);

            _gridStatus = Ui.Grid();
            root.Controls.Add(_gridStatus, 0, 3);
            root.SetColumnSpan(_gridStatus, 2);

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

            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Ui.Bg,
                Padding = new Padding(4, 8, 4, 4)
            };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            flow.Controls.Add(Ui.Btn("Обновить", (s, e) => RefreshStatus()));
            flow.Controls.Add(Ui.Btn("Установить службу", async (s, e) => await InstallMonitoringServiceAsync(), primary: true));
            flow.Controls.Add(Ui.Btn("Запустить", async (s, e) => await ControlMonitoringServiceAsync(true)));
            flow.Controls.Add(Ui.Btn("Остановить", async (s, e) => await ControlMonitoringServiceAsync(false)));
            flow.Controls.Add(Ui.Btn("Удалить службу", async (s, e) => await UninstallMonitoringServiceAsync(), danger: true));
            flow.Controls.Add(Ui.Btn("Открыть логи", (s, e) => OpenLogFolder()));
            bar.Controls.Add(flow);

            tab.Controls.Add(root);
            tab.Controls.Add(bar);
        }

        private static void AddStatusRow(TableLayoutPanel root, int row, string caption, Label value)
        {
            var lbl = new Label
            {
                Text = caption,
                AutoSize = true,
                Font = Ui.SectionFont,
                ForeColor = Ui.Muted,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 8, 0)
            };
            value.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            value.Margin = new Padding(0, 6, 0, 6);
            root.Controls.Add(lbl, 0, row);
            root.Controls.Add(value, 1, row);
        }

        private static Label MakeInfoLabel()
        {
            return new Label
            {
                AutoSize = false,
                Height = 24,
                Text = "—",
                Font = Ui.SectionFont,
                ForeColor = Ui.Text
            };
        }

        // =====================================================================
        // Settings tab
        // =====================================================================

        private void BuildSettingsTab(TabPage tab)
        {
            Ui.StyleTab(tab);

            var outer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                BackColor = Ui.Bg,
                Padding = new Padding(4)
            };
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            outer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            outer.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var title = new Label
            {
                Text = "Профили мониторинга служб",
                Font = Ui.TitleFont,
                ForeColor = Ui.Text,
                AutoSize = true,
                Margin = new Padding(4, 4, 0, 8)
            };
            outer.Controls.Add(title, 0, 0);

            // global boot delay card
            var globalCard = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 3,
                BackColor = Ui.Surface,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 0, 0, 8)
            };
            globalCard.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            globalCard.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            globalCard.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            globalCard.Controls.Add(new Label
            {
                Text = "Задержка после загрузки (мин):",
                AutoSize = true,
                Font = Ui.SectionFont,
                ForeColor = Ui.Text,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 12, 0)
            }, 0, 0);
            _numBootDelay = Ui.Num(0, 1440, 5);
            _numBootDelay.Margin = new Padding(0, 4, 16, 4);
            globalCard.Controls.Add(_numBootDelay, 1, 0);
            globalCard.Controls.Add(new Label
            {
                Text = "Общая: сначала ждём этот интервал, затем проверяем все службы из списка.",
                AutoSize = true,
                Font = Ui.BodyFont,
                ForeColor = Ui.Muted,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 8, 0, 0)
            }, 2, 0);
            outer.Controls.Add(globalCard, 0, 1);

            // list | detail — no Panel*MinSize (startup crash)
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 300,
                FixedPanel = FixedPanel.Panel1,
                BackColor = Ui.Bg,
                Margin = new Padding(0)
            };

            var left = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Ui.Surface,
                Padding = new Padding(10, 10, 10, 10),
                Margin = new Padding(0, 0, 8, 0)
            };
            var listTitle = Ui.Section("Службы в мониторинге");
            listTitle.Dock = DockStyle.Top;
            listTitle.Height = 24;

            _listServices = new ListBox
            {
                Dock = DockStyle.Fill,
                DisplayMember = "Title",
                IntegralHeight = false,
                BorderStyle = BorderStyle.FixedSingle,
                Font = Ui.BodyFont,
                BackColor = Ui.Surface
            };
            _listServices.SelectedIndexChanged += (s, e) => ShowSelectedService();

            var listMenu = new ContextMenuStrip { Font = Ui.BodyFont };
            var miDelete = new ToolStripMenuItem("Удалить из мониторинга");
            miDelete.Click += (s, e) => DeleteSelectedService();
            var miToggle = new ToolStripMenuItem("Отключить / включить");
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
                Height = 48,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = Ui.Surface
            };
            listBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            listBtns.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));

            _btnAddSvc = Ui.Btn("Добавить", (s, e) => AddService());
            _btnAddSvc.Dock = DockStyle.Fill;
            _btnAddSvc.Margin = new Padding(0, 0, 4, 0);
            _btnAddSvc.Height = Ui.BtnHeight;

            _btnDeleteSvc = Ui.Btn("Удалить", (s, e) => DeleteSelectedService(), danger: true);
            _btnDeleteSvc.Dock = DockStyle.Fill;
            _btnDeleteSvc.Margin = new Padding(4, 0, 0, 0);
            _btnDeleteSvc.Height = Ui.BtnHeight;

            listBtns.Controls.Add(_btnAddSvc, 0, 0);
            listBtns.Controls.Add(_btnDeleteSvc, 1, 0);

            var tip = new ToolTip { InitialDelay = 400, ShowAlways = true };
            tip.SetToolTip(_btnAddSvc, "Добавить службу 1С в список мониторинга");
            tip.SetToolTip(_btnDeleteSvc, "Убрать службу из списка (Windows-служба не удаляется)");
            tip.SetToolTip(_listServices, "ЛКМ — выбрать · ПКМ — удалить или отключить");

            left.Controls.Add(_listServices);
            left.Controls.Add(listBtns);
            left.Controls.Add(listTitle);

            _detailPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Ui.Surface,
                Padding = new Padding(14, 10, 14, 10),
                Margin = new Padding(8, 0, 0, 0)
            };
            BuildDetailControls(_detailPanel);

            split.Panel1.Controls.Add(left);
            split.Panel2.Controls.Add(_detailPanel);
            outer.Controls.Add(split, 0, 2);

            // action bar
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Height = 52,
                Padding = new Padding(4, 8, 4, 4),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                BackColor = Ui.Bg
            };
            _btnSave = Ui.Btn("Сохранить настройки", async (s, e) => await SaveSettingsAsync(), primary: true);
            var btnReload = Ui.Btn("Загрузить текущие", (s, e) => LoadConfigUi());
            var btnTest = Ui.Btn("Проверить выбранную", async (s, e) => await TestCheckAsync());
            var btnRestart = Ui.Btn("Перезапустить выбранную", async (s, e) => await RestartFromSettingsAsync());
            btnPanel.Controls.Add(_btnSave);
            btnPanel.Controls.Add(btnReload);
            btnPanel.Controls.Add(btnTest);
            btnPanel.Controls.Add(btnRestart);
            outer.Controls.Add(btnPanel, 0, 3);

            tab.Controls.Add(outer);
        }

        private void BuildDetailControls(Panel parent)
        {
            var head = new Label
            {
                Text = "Параметры выбранной службы",
                Font = Ui.TitleFont,
                ForeColor = Ui.Text,
                AutoSize = true,
                Dock = DockStyle.Top,
                Height = 32,
                Margin = new Padding(0, 0, 0, 8)
            };
            parent.Controls.Add(head);

            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                ColumnCount = 2,
                Padding = new Padding(0, 4, 0, 4)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
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
                    Font = Ui.BodyFont,
                    ForeColor = Ui.Muted,
                    Anchor = AnchorStyles.Left,
                    Margin = new Padding(0, 10, 12, 0)
                };
                c.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
                c.Margin = new Padding(0, 6, 0, 6);
                if (c is TextBox || c is ComboBox || c is NumericUpDown)
                    c.Height = Ui.InputHeight;
                tlp.Controls.Add(lbl, 0, row);
                tlp.Controls.Add(c, 1, row);
                row++;
            }

            _txtDisplayName = new TextBox { Dock = DockStyle.Fill, Font = Ui.BodyFont };
            Row("Отображаемое имя:", _txtDisplayName);

            _cbTargetService = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDown,
                AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                AutoCompleteSource = AutoCompleteSource.ListItems,
                Dock = DockStyle.Fill,
                Font = Ui.BodyFont
            };
            var pickPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, Height = 34 };
            _cbTargetService.Width = 300;
            _cbTargetService.Height = Ui.InputHeight;
            var btnPick = Ui.Btn("Из списка…", (s, e) => PickService());
            btnPick.Height = Ui.InputHeight;
            btnPick.Margin = new Padding(8, 2, 0, 0);
            pickPanel.Controls.Add(_cbTargetService);
            pickPanel.Controls.Add(btnPick);
            Row("Служба Windows (Name):", pickPanel);

            _cbCheckType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Dock = DockStyle.Fill,
                Font = Ui.BodyFont,
                Height = Ui.InputHeight
            };
            _cbCheckType.Items.AddRange(new object[]
            {
                "Статус службы (Running)",
                "Статус + TCP-порт",
                "Статус + HTTP-адрес"
            });
            Row("Тип проверки:", _cbCheckType);

            _txtTcpHost = new TextBox { Dock = DockStyle.Fill, Font = Ui.BodyFont, Text = "localhost" };
            Row("TCP хост:", _txtTcpHost);

            _numTcpPort = Ui.Num(1, 65535, 1541);
            Row("TCP порт:", _numTcpPort);

            _txtHttpUrl = new TextBox { Dock = DockStyle.Fill, Font = Ui.BodyFont, Text = "http://localhost/health" };
            Row("HTTP URL:", _txtHttpUrl);

            _numInterval = Ui.Num(5, 86400, 30);
            Row("Интервал проверки (сек):", _numInterval);

            _numTimeout = Ui.Num(1, 300, 10);
            Row("Таймаут ответа (сек):", _numTimeout);

            _chkSvcEnabled = new CheckBox
            {
                Text = "Мониторинг этой службы включён",
                AutoSize = true,
                Checked = true,
                Font = Ui.BodyFont
            };
            Row("Включение:", _chkSvcEnabled);

            _chkAutoRestart = new CheckBox
            {
                Text = "Автоперезапуск при отказе",
                AutoSize = true,
                Checked = true,
                Font = Ui.BodyFont
            };
            Row("Перезапуск:", _chkAutoRestart);

            _numMaxRestarts = Ui.Num(1, 100, 5);
            Row("Макс. перезапусков подряд:", _numMaxRestarts);

            _numCooldown = Ui.Num(0, 1440, 5);
            Row("Пауза между перезапусками (мин):", _numCooldown);

            parent.Controls.Add(tlp);
        }

        // =====================================================================
        // Log tab
        // =====================================================================

        private void BuildLogTab(TabPage tab)
        {
            Ui.StyleTab(tab);

            var bar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 52,
                BackColor = Ui.Bg,
                Padding = new Padding(4, 8, 4, 4)
            };
            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };
            flow.Controls.Add(Ui.Btn("Обновить журнал", (s, e) => LoadLog(), primary: true));
            flow.Controls.Add(Ui.Btn("Открыть папку", (s, e) => OpenLogFolder()));
            bar.Controls.Add(flow);

            var card = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Ui.Surface,
                Padding = new Padding(10),
                Margin = new Padding(0, 0, 0, 4)
            };

            _txtLog = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Dock = DockStyle.Fill,
                Font = Ui.MonoFont,
                BackColor = Ui.Surface,
                ForeColor = Ui.Text,
                BorderStyle = BorderStyle.FixedSingle
            };

            card.Controls.Add(_txtLog);
            tab.Controls.Add(card);
            tab.Controls.Add(bar);
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
