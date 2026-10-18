// Flato3D-SR installer: lists the installed games (Steam, Heroic), shows which ones Flato3D knows
// (profiles) or expects to work (candidate list), and installs or removes the stereo depth runtime.
// Command line (for tests): --scan <log> | --detect <exe> --log <log> |
//   --install <exe> [--route opengl|d3d9|d3d11] [--output auto|sbs|tab] [--separation N] --log <log> |
//   --uninstall <exe> --log <log>
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Flato3D
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            if (!Payload.Find())
            {
                string msg = L.T("data\\game_profiles.json을 찾지 못했습니다. 설치기를 Flato3D-SR 폴더 안에서 실행하세요.",
                                 "data\\game_profiles.json not found. Run the installer from inside the Flato3D-SR folder.");
                if (args.Length > 0) { Console.Error.WriteLine(msg); return 2; }
                MessageBox.Show(msg, "Flato3D-SR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 2;
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length > 1 && args[0] == "--screenshot") return Screenshot(args[1], args.Length > 2 ? args[2] : null);
            if (args.Length > 0) return Cli(args);
            Application.Run(new MainForm());
            return 0;
        }

        // Renders the main window off-screen into a PNG (checking the look without taking the user's screen);
        // game: title of the game to select first.
        static int Screenshot(string png, string game)
        {
            using (var f = new MainForm { StartPosition = FormStartPosition.Manual, Location = new Point(-30000, -30000), ShowInTaskbar = false })
            {
                f.Show();
                Application.DoEvents();
                f.SelectForScreenshot(game);
                Application.DoEvents();
                using (var bmp = new Bitmap(f.Width, f.Height))
                {
                    f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height));
                    bmp.Save(png, System.Drawing.Imaging.ImageFormat.Png);
                }
            }
            return 0;
        }

        static string Arg(string[] a, string name)
        {
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }

        static Game GameFor(string exe)
        {
            string full = Path.GetFullPath(exe);
            var g = Library.Scan().FirstOrDefault(x => full.StartsWith(x.Dir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
            if (g != null) return g;
            g = new Game { Title = Path.GetFileName(Path.GetDirectoryName(full)), Store = "", Dir = Path.GetDirectoryName(full) };
            Library.Match(g);
            return g;
        }

        static int Cli(string[] a)
        {
            var log = new StringBuilder();
            int rc = 0;
            try
            {
                if (a[0] == "--scan")
                {
                    foreach (var g in Library.Scan())
                    {
                        string exe = Library.FindExe(g), why;
                        string r = Library.DetectRoute(g, exe, out why);
                        System.Drawing.Color? mine;
                        log.AppendLine(string.Join(" | ", g.Title, g.Store, MainForm.StatusFor(g, exe, out mine), exe == null ? "-" : Pe.Arch(exe) ?? "?", r, why,
                                                   Setup.Status(exe == null ? null : Path.GetDirectoryName(exe)), exe ?? g.Dir));
                    }
                    File.WriteAllText(a.Length > 1 ? a[1] : "flato3d_scan.txt", log.ToString(), Encoding.UTF8);
                    return 0;
                }
                string exePath = a.Length > 1 ? Path.GetFullPath(a[1]) : null;
                var game = GameFor(exePath);
                string why2;
                string route = Arg(a, "--route") ?? Library.DetectRoute(game, exePath, out why2);
                if (a[0] == "--detect")
                {
                    Library.DetectRoute(game, exePath, out why2);
                    log.AppendLine("game: " + game.Title + " | " + Library.StatusText(game) + " | profile " + (Json.Str(game.Profile, "id") ?? "-"));
                    log.AppendLine("exe: " + exePath + " | " + Pe.Arch(exePath) + " | imports " + string.Join(",", Pe.Imports(exePath).Where(n => n.StartsWith("d3d") || n.StartsWith("dxgi") || n.StartsWith("opengl") || n.StartsWith("vulkan") || n.StartsWith("ddraw"))));
                    log.AppendLine("route: " + route + " (" + why2 + ") | status: " + Setup.Status(Path.GetDirectoryName(exePath)));
                    var rec = Recommend.For(game, exePath);
                    log.AppendLine("auto: depth " + (rec.Flat ? rec.Rule : "plain stereo") + " (" + rec.RuleWhy + ") | output " + rec.Output + " (" + rec.OutputWhy +
                                   ") | upscale " + (rec.Upscale ? 1 : 0) + " (" + rec.UpscaleWhy + ") | separation " + rec.Separation.ToString(CultureInfo.InvariantCulture));
                }
                else if (a[0] == "--install")
                {
                    var rec = Recommend.For(game, exePath);   // what is not given on the command line
                    var o = new Options { Output = Arg(a, "--output") ?? rec.Output, Separation = rec.Separation, Upscale = rec.Upscale };
                    string sep = Arg(a, "--separation");
                    if (sep != null) o.Separation = double.Parse(sep, CultureInfo.InvariantCulture);
                    if (Arg(a, "--upscale") != null) o.Upscale = Arg(a, "--upscale") == "1";
                    log.AppendLine("auto: output " + rec.Output + " (" + rec.OutputWhy + "), upscale " + (rec.Upscale ? 1 : 0) + " (" + rec.UpscaleWhy + ")");
                    log.Append(Setup.Install(game, exePath, route, o));
                }
                else if (a[0] == "--report")
                {
                    string summary;
                    var images = a.Select((x, i) => x == "--image" && i + 1 < a.Length ? a[i + 1] : null).Where(x => x != null).ToList();
                    string result = Arg(a, "--result") ?? "issues";
                    log.AppendLine("report: " + Report.Create(game, exePath, result, Arg(a, "--symptom"), images, out summary));
                    if (exePath != null) Setup.RecordUserStatus(Path.GetDirectoryName(exePath), result, Arg(a, "--symptom"));
                }
                else if (a[0] == "--uninstall")
                    log.Append(Setup.Uninstall(Path.GetDirectoryName(exePath)));
                else { log.AppendLine("unknown command " + a[0]); rc = 1; }
            }
            catch (Exception e) { log.AppendLine("ERROR: " + e.Message); rc = 1; }
            File.WriteAllText(Arg(a, "--log") ?? "flato3d_installer.log", log.ToString(), Encoding.UTF8);
            return rc;
        }
    }

    class MainForm : Form
    {
        readonly ListView games = new ListView(), cands = new ListView();
        readonly CheckBox upscaleBox = new CheckBox();
        readonly ToolTip tips = new ToolTip { AutoPopDelay = 20000 };
        readonly TextBox exeBox = new TextBox(), logBox = new TextBox(), candFilter = new TextBox();
        readonly Label info = new Label();
        readonly ComboBox routeBox = new ComboBox(), outputBox = new ComboBox();
        readonly NumericUpDown sepBox = new NumericUpDown();
        readonly Button installBtn = new Button(), removeBtn = new Button(), runBtn = new Button(), folderBtn = new Button(), logBtn = new Button(), reportBtn = new Button(), defenderBtn = new Button();
        List<Game> all = new List<Game>();
        Game current;
        string currentExe;
        static readonly string[] Routes = { Route.OpenGL, Route.D3D9, Route.D3D11 };
        static readonly string[] Outputs = { "auto", "sbs", "tab" };
        float scale = 1f;   // the process is DPI aware: pixel sizes below are for 96 dpi and get scaled
        int P(int px) { return (int)Math.Round(px * scale); }

        public MainForm()
        {
            Text = "Flato3D-SR " + L.T("설치기", "installer");
            using (var gfx = CreateGraphics()) scale = gfx.DpiX / 96f;
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(P(1180), P(780));
            MinimumSize = new Size(P(900), P(600));
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Theme.TitleBar(this);

            var t1 = new Panel { Dock = DockStyle.Fill };
            var t2 = new Panel { Dock = DockStyle.Fill };
            var t3 = new Panel { Dock = DockStyle.Fill };
            var pages = new Panel { Dock = DockStyle.Fill, Padding = new Padding(P(6)) };
            pages.Controls.AddRange(new Control[] { t1, t2, t3 });
            Controls.Add(pages);
            Controls.Add(BuildHeader(new[] { L.T("게임", "Games"), L.T("후보 목록", "Candidate list"), L.T("도움말", "Help") }, new Control[] { t1, t2, t3 }));
            BuildGamesTab(t1);
            BuildCandidatesTab(t2);
            BuildHelpTab(t3);
            Theme.Primary(installBtn);
            Theme.Apply(this);
            Shown += (s, e) => Rescan();
        }

        // Header bar in the icon's colours: the icon, "Flato" in coral and "3D" in teal, then the pages as tabs
        // (the active one underlined in coral). Replaces the system tab control, which cannot be coloured.
        Control BuildHeader(string[] names, Control[] pages)
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = P(50), WrapContents = false, BackColor = Theme.Header, Padding = new Padding(P(12), 0, 0, 0) };
            var logo = new PictureBox { Size = new Size(P(32), P(32)), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, P(9), P(8), 0), Image = Theme.AppIcon(P(32)),
                                        BackColor = Theme.Header };
            var name = new Theme.Wordmark(new Font("Segoe UI Semibold", 14f)) { Margin = new Padding(0, P(12), P(30), 0) };
            bar.Controls.AddRange(new Control[] { logo, name });
            var tabs = new List<Label>();
            Action<int> select = i =>
            {
                for (int k = 0; k < pages.Length; ++k)
                {
                    pages[k].Visible = k == i;
                    tabs[k].ForeColor = k == i ? Theme.Text : Theme.Dim;
                    tabs[k].Invalidate();
                }
            };
            for (int i = 0; i < names.Length; ++i)
            {
                int idx = i;
                var t = new Label { Text = names[i], AutoSize = true, Font = new Font("Segoe UI", 10.5f), Padding = new Padding(P(10), P(15), P(10), P(11)),
                                    Margin = new Padding(0), Cursor = Cursors.Hand, ForeColor = Theme.Dim };
                t.Click += (s, e) => select(idx);
                t.Paint += (s, e) =>
                {
                    if (!pages[idx].Visible) return;
                    using (var b = new SolidBrush(Theme.Coral)) e.Graphics.FillRectangle(b, P(8), t.Height - P(3), t.Width - P(16), P(3));
                };
                tabs.Add(t);
                bar.Controls.Add(t);
            }
            select(0);
            return bar;
        }

        void BuildGamesTab(Control t)
        {
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(P(6), P(6), P(6), P(2)) };
            var refresh = new Button { Text = L.T("새로 고침", "Refresh"), AutoSize = true };
            refresh.Click += (s, e) => Rescan();
            var browse = new Button { Text = L.T("게임 추가 (exe 고르기)...", "Add a game (choose its exe)..."), AutoSize = true };
            browse.Click += (s, e) => BrowseExe();
            tips.SetToolTip(browse, L.T("목록에 없는 게임의 exe를 고르면 32/64비트와 경로를 판별합니다. 설치하면 목록에 나오고, [상태 보고]로 결과를 알려 주면 프로필이 만들어집니다.",
                                        "Choose the exe of a game the list does not show: 32/64-bit and the route are detected. Installed, it shows in the list; [Report status] turns it into a profile."));
            top.Controls.AddRange(new Control[] { refresh, browse, new Label { AutoSize = true, Padding = new Padding(P(10), P(6), 0, 0), ForeColor = Theme.Dim,
                Text = L.T("Flato3D로 할 수 있는 평면 2D 게임만 나옵니다. 목록에 없는 게임은 [게임 추가]로 exe를 고르세요.", "Only flat 2D games Flato3D can do are listed; add others with [Add a game].") } });

            games.View = View.Details;
            games.FullRowSelect = true;
            games.HideSelection = false;
            games.MultiSelect = false;
            games.Dock = DockStyle.Fill;
            games.Columns.Add(L.T("게임", "Game"), P(300));
            games.Columns.Add(L.T("스토어", "Store"), P(60));
            games.Columns.Add(L.T("상태", "Status"), P(150));
            games.Columns.Add("Flato3D", P(170));
            games.Columns.Add(L.T("폴더", "Folder"), P(460));
            games.SelectedIndexChanged += (s, e) => { if (games.SelectedItems.Count > 0) ShowGame((Game)games.SelectedItems[0].Tag, null); };

            var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = P(380), ColumnCount = 1, Padding = new Padding(P(6)) };
            info.AutoSize = false;
            info.Dock = DockStyle.Fill;

            var row1 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            exeBox.ReadOnly = true;
            exeBox.Width = P(820);
            var change = new Button { Text = L.T("exe 변경...", "Change exe..."), AutoSize = true };
            change.Click += (s, e) => ChangeExe();
            row1.Controls.AddRange(new Control[] { new Label { Text = L.T("실행 파일:", "Executable:"), AutoSize = true, Padding = new Padding(0, P(6), 0, 0) }, exeBox, change });

            var row2 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            routeBox.DropDownStyle = ComboBoxStyle.DropDownList;
            routeBox.Width = P(230);
            foreach (string r in Routes) routeBox.Items.Add(Route.Label(r));
            // a route chosen by hand overrides "not possible" (e.g. a game that takes an OpenGL switch)
            routeBox.SelectionChangeCommitted += (s, e) => { if (currentExe != null) installBtn.Enabled = true; };
            outputBox.DropDownStyle = ComboBoxStyle.DropDownList;
            outputBox.Width = P(230);
            outputBox.Items.AddRange(new object[] { L.T("SR 디스플레이 (Acer, Samsung)", "SR display (Acer, Samsung)"), L.T("좌우 SBS", "Side by side"), L.T("위아래", "Top and bottom") });
            sepBox.Minimum = 1;
            sepBox.Maximum = 200;
            sepBox.DecimalPlaces = 1;
            sepBox.Width = P(70);
            upscaleBox.Text = L.T("업스케일", "Upscale");
            upscaleBox.AutoSize = true;
            upscaleBox.Padding = new Padding(P(12), P(4), 0, 0);
            tips.SetToolTip(upscaleBox, L.T("게임이 기본 해상도(예: 4K)를 지원하지 않거나 글씨가 너무 작을 때 켭니다.\n게임은 1920×1080으로 두면, 화면에는 기본 해상도로 확대해 보여 줍니다.",
                                            "For games that cannot use the native resolution (e.g. 4K) or whose text gets too small.\nSet the game to 1920x1080; it is shown enlarged at the native resolution."));
            row2.Controls.AddRange(new Control[] {
                new Label { Text = L.T("경로:", "Route:"), AutoSize = true, Padding = new Padding(0, P(6), 0, 0) }, routeBox,
                new Label { Text = L.T("출력:", "Output:"), AutoSize = true, Padding = new Padding(P(12), P(6), 0, 0) }, outputBox,
                new Label { Text = L.T("분리값(%):", "Separation (%):"), AutoSize = true, Padding = new Padding(P(12), P(6), 0, 0) }, sepBox,
                upscaleBox });

            var row3 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            installBtn.Text = L.T("설치", "Install");
            removeBtn.Text = L.T("제거", "Uninstall");
            runBtn.Text = L.T("실행", "Run");
            folderBtn.Text = L.T("폴더 열기", "Open folder");
            logBtn.Text = L.T("로그 보기", "Show log");
            reportBtn.Text = L.T("상태 보고", "Report status");
            tips.SetToolTip(reportBtn, L.T("이 게임이 잘 되는지, 문제가 있는지 로그, 설정, 스크린샷과 함께 보고합니다.", "Report whether this game works or has problems, with its logs, settings and screenshots."));
            defenderBtn.Text = L.T("Defender 예외", "Defender exclusion");
            tips.SetToolTip(defenderBtn, L.T("Windows Defender가 Flato3D 로더(version.dll)를 잘못 막을 때, 이 게임의 version.dll만 예외로 등록합니다.",
                                             "When Windows Defender wrongly blocks the Flato3D loader (version.dll), exclude this game's version.dll only."));
            foreach (var b in new[] { installBtn, removeBtn, runBtn, folderBtn, logBtn, reportBtn, defenderBtn }) { b.AutoSize = true; b.MinimumSize = new Size(P(90), P(28)); row3.Controls.Add(b); }
            reportBtn.Click += (s, e) => DoReport();
            defenderBtn.Click += (s, e) => DoDefender();
            installBtn.Click += (s, e) => DoInstall();
            removeBtn.Click += (s, e) => DoUninstall();
            runBtn.Click += (s, e) => DoRun();
            folderBtn.Click += (s, e) => { if (currentExe != null) Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(currentExe) + "\""); };
            logBtn.Click += (s, e) =>
            {
                string f = currentExe == null ? null : Path.Combine(Path.GetDirectoryName(currentExe), "OGL3D.log");
                if (f != null && File.Exists(f)) Process.Start("notepad.exe", "\"" + f + "\"");
                else logBox.Text = L.T("로그(OGL3D.log)가 아직 없습니다. 설치한 뒤 게임을 한 번 실행하면 생깁니다.", "No log (OGL3D.log) yet: it appears once the game has run with Flato3D installed.");
            };

            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill;

            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, P(150)));
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            bottom.Controls.Add(info, 0, 0);
            bottom.Controls.Add(row1, 0, 1);
            bottom.Controls.Add(row2, 0, 2);
            bottom.Controls.Add(row3, 0, 3);
            bottom.Controls.Add(logBox, 0, 4);

            t.Controls.Add(games);
            t.Controls.Add(bottom);
            t.Controls.Add(top);
            EnableActions(false);
        }

        void BuildCandidatesTab(Control t)
        {
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(P(6), P(6), P(6), P(2)) };
            candFilter.Width = P(300);
            candFilter.TextChanged += (s, e) => FillCandidates();
            top.Controls.AddRange(new Control[] { new Label { Text = L.T("검색:", "Search:"), AutoSize = true, Padding = new Padding(0, P(4), 0, 0) }, candFilter,
                new Label { Text = L.T("Flato3D로 할 수 있는 평면 2D 후보입니다. 경로가 있어도 게임마다 시험이 필요합니다. 일반 입체 게임은 Geo Gaming Hub에서 다룹니다.",
                                       "Flat 2D candidates Flato3D can do; each still needs testing. Ordinary stereo games are in Geo Gaming Hub."), AutoSize = true, Padding = new Padding(P(12), P(4), 0, 0) } });
            cands.View = View.Details;
            cands.FullRowSelect = true;
            cands.Dock = DockStyle.Fill;
            cands.Columns.Add(L.T("게임", "Game"), P(300));
            cands.Columns.Add("API", P(80));
            cands.Columns.Add(L.T("경로", "Route"), P(180));
            cands.Columns.Add(L.T("종류", "Kind"), P(90));
            cands.Columns.Add(L.T("상태", "Status"), P(80));
            cands.Columns.Add(L.T("메모", "Note"), P(460));
            t.Controls.Add(cands);
            t.Controls.Add(top);
            FillCandidates();
        }

        void BuildHelpTab(Control t)
        {
            var box = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 10f) };
            box.Text = string.Join("\r\n", new[] {
                L.T("Flato3D-SR은 평면 2D 게임(픽셀 아트, 손그림)의 레이어와 스프라이트마다 깊이를 줘서 입체로 보여 줍니다.", "Flato3D-SR gives each layer and sprite of a flat 2D game its own depth."),
                "",
                L.T("동작 방식", "How it works"),
                L.T("  OpenGL 게임: opengl32.dll(OGL3D-SR)이 두 눈을 그립니다.", "  OpenGL games: opengl32.dll (OGL3D-SR) renders both eyes."),
                L.T("  Direct3D 8~11 게임: WineD3D가 OpenGL로 바꾸고, version.dll 로더가 게임을 WineD3D로 보냅니다.", "  Direct3D 8-11 games: WineD3D turns them into OpenGL; the version.dll loader sends the game there."),
                L.T("  Direct3D 12, Vulkan, 완성 화면 한 장만 내는 게임(DOSBox, ScummVM, 에뮬레이터)은 할 수 없습니다.", "  Direct3D 12, Vulkan and games that output one finished image (DOSBox, ScummVM, emulators) are not possible."),
                L.T("  원근이 있는 일반 입체 게임(2.5D, 3D)은 여기서 다루지 않습니다. Geo Gaming Hub에서 설치합니다.", "  Games with real depth (2.5D, 3D) are not done here: install them with Geo Gaming Hub."),
                "",
                L.T("알아 둘 것", "Good to know"),
                L.T("  NVIDIA GPU에서만 동작을 보장합니다. Intel GPU에서는 셰이더가 깨진 적이 있습니다. NVIDIA와 다른 GPU가 함께 있는 노트북은 설치할 때 게임을 NVIDIA GPU로 실행하도록 설정합니다.",
                    "  Only NVIDIA GPUs are supported; on Intel GPUs shaders have broken. On laptops with NVIDIA and another GPU, install sets the game to run on the NVIDIA GPU."),
                L.T("  Windows Defender가 Direct3D 게임용 로더(version.dll)를 바이러스로 잘못 진단할 수 있습니다(예: Trojan:Win32/Cloxer). 악성 코드가 아니며, 막히면 3D가 나오지 않습니다.",
                    "  Windows Defender may wrongly flag the loader for Direct3D games (version.dll) as malware (e.g. Trojan:Win32/Cloxer). It is not; when blocked there is no 3D."),
                L.T("    [Defender 예외]를 누르면 그 게임의 version.dll만 예외로 등록합니다(관리자 권한 확인). 제거할 때 예외도 지울 수 있습니다.",
                    "    [Defender exclusion] adds that game's version.dll only (Windows asks for permission); uninstalling offers to remove it."),
                L.T("  다른 도구(geo-11, dgVoodoo, ReShade)의 DLL은 설치할 때 Flato3D_backup 폴더로 옮기고, 제거하면 되돌립니다.", "  Other tools' DLLs (geo-11, dgVoodoo, ReShade) go to Flato3D_backup on install and come back on uninstall."),
                L.T("  업스케일: 게임이 기본 해상도(예: 4K)를 못 쓰거나 글씨가 너무 작으면 켜고, 게임은 1920×1080으로 둡니다.", "  Upscale: for games that cannot use the native resolution (e.g. 4K) or whose text is too small; set the game to 1920x1080."),
                L.T("    게임은 그 해상도로 그리고, 화면에는 기본 해상도로 확대해 보여 줍니다. 해상도 변경은 막아 화면은 기본 해상도를 유지합니다.", "    The game renders at that size and is shown enlarged at the native resolution; display mode changes are blocked."),
                L.T("  상태 보고: 결과(잘 됨 / 일부 문제 / 안 됨)를 고르고 메모와 스크린샷(파일 추가, Ctrl+V)을 넣습니다. 로그, 설정, 마지막 Ctrl+F12 덤프가 함께 한 폴더에 모이고, 결과는 게임 목록에 표시됩니다. 원하면 GitHub 이슈 페이지가 열립니다.",
                    "  Report status: choose the result (works / some problems / does not work), add a note and screenshots (files, Ctrl+V); logs, settings and the last Ctrl+F12 dump go into one folder, the result shows in the game list, optionally a GitHub issue."),
                "",
                L.T("게임 안의 단축키", "Hotkeys in game"),
                L.T("  Ctrl+F3 / Ctrl+F4  분리값 낮춤 / 높임", "  Ctrl+F3 / Ctrl+F4  separation down / up"),
                L.T("  Ctrl+F5 / Ctrl+F6  화면 위치를 앞으로 / 뒤로", "  Ctrl+F5 / Ctrl+F6  screen plane nearer / farther"),
                L.T("  Ctrl+F7  값 저장      Ctrl+T  3D 켜기/끄기      Ctrl+Alt+W  SR 디스플레이 출력 켜기/끄기", "  Ctrl+F7  save      Ctrl+T  3D on/off      Ctrl+Alt+W  SR display output on/off"),
                L.T("  Ctrl+F12  두 눈 이미지 저장(분석용). 이 키들은 게임에 전달되지 않습니다.", "  Ctrl+F12  save both eyes (analysis). The game does not receive these keys."),
                "",
                L.T("출처와 라이선스", "Credits and license"),
                L.T("  Flato3D-SR, OGL3D-SR: MIT (nautymac). WineD3D: Wine 프로젝트(LGPL), Windows 빌드 Federico Dossena.", "  Flato3D-SR, OGL3D-SR: MIT (nautymac). WineD3D: the Wine project (LGPL), Windows builds by Federico Dossena."),
                L.T("  아이디어 참고: masterotaku의 Castlevania Dominus 3D 수정(코드는 쓰지 않음). 후보 자료: PCGamingWiki(CC BY-NC-SA).", "  Idea: masterotaku's Castlevania Dominus 3D fix (no code used). Candidate data: PCGamingWiki (CC BY-NC-SA)."),
                "  https://github.com/nautymac/Flato3D-SR" });
            t.Controls.Add(box);
        }

        void Rescan()
        {
            Cursor = Cursors.WaitCursor;
            all = Library.Scan();
            Cursor = Cursors.Default;
            FillGames();
        }

        void FillGames()
        {
            games.BeginUpdate();
            games.Items.Clear();
            foreach (var g in all)
            {
                // only Flato3D's games: a profile, a flat 2D candidate it can do, or a Flato3D install here.
                // Ordinary stereo games (real depth) are Geo Gaming Hub's; one-image / browser / D3D12-Vulkan
                // games cannot be done this way. Any other exe: "Choose an exe".
                string exe = Library.FindExe(g), dir = exe == null ? null : Path.GetDirectoryName(exe);
                string inst = Setup.Status(dir);
                bool flatoInstall = inst != "" && !Setup.PlainStereo(dir);
                bool candidate = g.Candidate != null && Json.Str(g.Candidate, "kind") != "1" &&
                                 !new[] { "oneimage", "browser", "unsupported" }.Contains(Json.Str(g.Candidate, "api"));
                if (g.Profile == null && !candidate && !flatoInstall) continue;
                Color? mine;
                string st = StatusFor(g, exe, out mine);
                var it = new ListViewItem(new[] { g.Title, g.Store, st, inst, g.Dir }) { Tag = g };
                if (mine != null) it.ForeColor = mine.Value;
                else if (g.Profile != null) it.ForeColor = Theme.Profile;
                games.Items.Add(it);
            }
            games.EndUpdate();
            info.Text = L.T("게임을 고르세요. 청록 = 확인된 프로필이 있는 게임, 상태 열의 \"내 확인\" = 직접 보고한 결과.",
                            "Choose a game. Teal = a tested profile; \"my check\" in the status column = your own report.");
        }

        void FillCandidates()
        {
            string f = candFilter.Text.Trim().ToLowerInvariant();
            cands.BeginUpdate();
            cands.Items.Clear();
            foreach (var c in Payload.Candidates)
            {
                string title = Json.Str(c, "title") ?? "";
                if (f.Length > 0 && !title.ToLowerInvariant().Contains(f)) continue;
                // only what Flato3D can do: not ordinary stereo games (Geo Gaming Hub), not one-image / browser / D3D12-Vulkan
                if (Json.Str(c, "kind") == "1" || Json.Str(c, "kind") == "4" ||
                    new[] { "oneimage", "browser", "unsupported" }.Contains(Json.Str(c, "api"))) continue;
                string api = Json.Str(c, "api") ?? "";
                string route = api == "opengl" ? Route.Label(Route.OpenGL) : api == "opengl_alt" ? L.T("OpenGL (전환 필요)", "OpenGL (switch needed)") :
                               api == "d3d9" ? Route.Label(Route.D3D9) : api == "d3d11" ? Route.Label(Route.D3D11) :
                               api == "oneimage" ? L.T("AI 깊이만", "AI depth only") : api == "browser" ? L.T("어려움", "hard") :
                               api == "unsupported" ? L.T("불가 (D3D12/Vulkan)", "no (D3D12/Vulkan)") : L.T("미확인", "unknown");
                string kind;
                switch (Json.Str(c, "kind")) { case "1": kind = L.T("2.5D/3D", "2.5D/3D"); break; case "2": kind = L.T("레이어 z", "layer z"); break; case "4": kind = L.T("한 장 화면", "one image"); break; default: kind = L.T("평면 레이어", "flat layers"); break; }
                cands.Items.Add(new ListViewItem(new[] { title, api, route, kind, Json.Str(c, "status") ?? "", Json.Str(c, "note") ?? "" }));
            }
            cands.EndUpdate();
        }

        void EnableActions(bool on)
        {
            foreach (Control c in new Control[] { installBtn, removeBtn, runBtn, folderBtn, logBtn, reportBtn, defenderBtn, routeBox, outputBox, sepBox, upscaleBox }) c.Enabled = on;
        }

        void ShowGame(Game g, string exe)
        {
            current = g;
            currentExe = exe ?? Library.FindExe(g);
            exeBox.Text = currentExe ?? L.T("(exe를 찾지 못함: exe 변경으로 고르세요)", "(no exe found: use Change exe)");
            string why;
            string r = Library.DetectRoute(g, currentExe, out why);
            string arch = currentExe == null ? "?" : Pe.Arch(currentExe) ?? "?";
            var rec = Recommend.For(g, currentExe);
            ApplyInstalled(rec, currentExe);
            sepBox.Value = (decimal)Math.Max(1, Math.Min(200, rec.Separation));
            int oi = Array.IndexOf(Outputs, rec.Output);
            outputBox.SelectedIndex = oi < 0 ? 0 : oi;
            upscaleBox.Checked = rec.Upscale;
            int ri = Array.IndexOf(Routes, r);
            routeBox.SelectedIndex = ri < 0 ? 2 : ri;
            var sb = new StringBuilder();
            Color? mineColor;
            sb.AppendLine(g.Title + "   [" + StatusFor(g, currentExe, out mineColor) + "]   " + Setup.Status(currentExe == null ? null : Path.GetDirectoryName(currentExe)));
            sb.AppendLine(L.T("판별: ", "Detected: ") + (r == Route.None ? L.T("불가", "not possible") : Route.Label(r)) + " — " + why + "   " + arch);
            sb.AppendLine(L.T("깊이: ", "Depth: ") + RuleLabel(rec) + " (" + rec.RuleWhy + ")   " +
                          L.T("출력: ", "Output: ") + outputBox.Items[outputBox.SelectedIndex] + " (" + rec.OutputWhy + ")");
            sb.AppendLine(L.T("업스케일: ", "Upscale: ") + (rec.Upscale ? L.T("켬", "on") : L.T("끔", "off")) + " (" + rec.UpscaleWhy + ")");
            foreach (var n in Json.Arr(g.Profile, "known_issues").Take(2)) sb.AppendLine("• " + n);
            if (g.Profile == null && Json.Str(g.Candidate, "note") is string note && note.Length > 0) sb.AppendLine("• " + note);
            foreach (var n in Json.Arr(g.Profile, "remove")) sb.AppendLine(L.T("• 설치 때 옮겨 둠: ", "• moved aside on install: ") + n);
            if (Gpu.Known && !Gpu.Nvidia) sb.AppendLine("⚠ " + Gpu.Warning());
            // set up by hand before the installer: no record to undo, so [Uninstall] waits for one
            if (currentExe != null && Setup.ManualInstall(Path.GetDirectoryName(currentExe)))
                sb.AppendLine(L.T("• 설치 기록이 없어 [제거]가 꺼져 있습니다. [설치]를 한 번 누르면 기록이 생기고(지금 설정은 유지), 그다음부터 [제거]를 쓸 수 있습니다.",
                                  "• No install record, so [Uninstall] is off: press [Install] once to create it (the current settings are kept), then [Uninstall] works."));
            info.Text = sb.ToString();
            EnableActions(currentExe != null);
            reportBtn.Enabled = true;   // also when the exe was not found or the game cannot be done
            // the loader is only installed on the WineD3D routes (also by hand, before the installer: no record)
            defenderBtn.Enabled = currentExe != null && r != Route.OpenGL && r != Route.None &&
                                  (File.Exists(Path.Combine(Path.GetDirectoryName(currentExe), Setup.ManifestName)) ||
                                   File.Exists(Path.Combine(Path.GetDirectoryName(currentExe), "version.dll")));
            installBtn.Enabled = currentExe != null && r != Route.None;
            removeBtn.Enabled = currentExe != null && File.Exists(Path.Combine(Path.GetDirectoryName(currentExe), Setup.ManifestName));
        }

        static string RuleLabel(Recommendation rec)
        {
            if (!rec.Flat) return L.T("일반 입체 게임 (Geo Gaming Hub)", "ordinary stereo game (Geo Gaming Hub)");
            return rec.Rule == "feet" ? L.T("발 위치 (어드벤처)", "feet (adventure)") : L.T("그리는 순서", "drawing order");
        }

        // An installed Flato3D setup shows its own values (also those saved in game with Ctrl+F7).
        static void ApplyInstalled(Recommendation rec, string exe)
        {
            string dir = exe == null ? null : Path.GetDirectoryName(exe);
            string ini = dir == null ? null : Path.Combine(dir, "OGL3D.ini");
            if (ini == null || !File.Exists(ini)) return;
            string text = File.ReadAllText(ini, Encoding.UTF8);
            if (!File.Exists(Path.Combine(dir, Setup.ManifestName)) && !Regex.IsMatch(text, @"^\[Flat\]", RegexOptions.Multiline)) return;
            string installed = L.T("설치된 설정", "installed setting");
            double sep;
            if (double.TryParse(Setup.IniGet(text, "Stereo", "separation"), NumberStyles.Float, CultureInfo.InvariantCulture, out sep)) rec.Separation = sep;
            string o = Setup.IniGet(text, "Stereo", "output");
            if (Array.IndexOf(Outputs, o) >= 0) { rec.Output = o; rec.OutputWhy = installed; }
            string u = Setup.IniGet(text, "Display", "upscale");
            if (u != null) { rec.Upscale = u == "1"; rec.UpscaleWhy = installed; }
        }

        void BrowseExe()
        {
            using (var d = new OpenFileDialog { Filter = L.T("실행 파일|*.exe", "Executables|*.exe") })
                if (d.ShowDialog(this) == DialogResult.OK) OpenExe(d.FileName);
        }

        // "Change exe": another exe of the selected game, or (nothing selected, or a file from another
        // folder) any game's exe - also games the list leaves out.
        void ChangeExe()
        {
            string start = current != null && Directory.Exists(current.Dir) ? current.Dir : "";
            using (var d = new OpenFileDialog { Filter = L.T("실행 파일|*.exe", "Executables|*.exe"), InitialDirectory = start })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                if (current != null && d.FileName.StartsWith(current.Dir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) ShowGame(current, d.FileName);
                else OpenExe(d.FileName);
            }
        }

        // The game an exe belongs to (a scanned one, or a new entry named after its folder), selected in the
        // list when the list shows it.
        void OpenExe(string file)
        {
            string dir = Path.GetDirectoryName(file);
            var g = all.FirstOrDefault(x => file.StartsWith(x.Dir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                    ?? new Game { Title = Path.GetFileName(dir), Store = "", Dir = dir };
            if (g.Profile == null && g.Candidate == null) Library.Match(g);
            foreach (ListViewItem it in games.Items)
                if (it.Tag == g) { it.Selected = true; it.EnsureVisible(); }
            ShowGame(g, file);
        }

        void DoInstall()
        {
            if (current == null || currentExe == null) return;
            var o = new Options { Output = Outputs[Math.Max(0, outputBox.SelectedIndex)], Separation = (double)sepBox.Value, Upscale = upscaleBox.Checked };
            string route = Routes[Math.Max(0, routeBox.SelectedIndex)];
            if (Process.GetProcessesByName(Path.GetFileNameWithoutExtension(currentExe)).Length > 0)
            {
                MessageBox.Show(this, L.T("게임이 실행 중입니다. 끄고 다시 하세요.", "The game is running. Close it first."), Text);
                return;
            }
            try { logBox.Text = Setup.Install(current, currentExe, route, o).Replace("\n", "\r\n"); }
            catch (Exception e) { logBox.Text = L.T("설치 실패: ", "Install failed: ") + e.Message; }
            RefreshGame(current);
        }

        class Shot
        {
            public string Path, Label;
            public override string ToString() { return Label; }
        }

        void DoReport()
        {
            if (current == null) return;
            bool pre;
            string folder = Report.Folder(out pre);
            using (var f = new Form { Text = L.T("상태 보고", "Report status") + " — " + current.Title, Font = Font, StartPosition = FormStartPosition.CenterParent,
                                      FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false, ShowInTaskbar = false, ClientSize = new Size(P(760), P(640)) })
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Padding = new Padding(P(10)) };
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
                layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

                // screenshots: ticked ones go into the report; recent captures are offered unticked
                var shots = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, HorizontalScrollbar = true };
                var preview = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
                var shotArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0) };
                shotArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
                shotArea.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
                shotArea.Controls.Add(shots, 0, 0);
                shotArea.Controls.Add(preview, 1, 0);
                Action<string, bool, string> addShot = (path, tick, tag) =>
                {
                    if (shots.Items.Cast<Shot>().Any(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase))) return;
                    shots.Items.Add(new Shot { Path = path, Label = tag + Path.GetFileName(path) + "  (" + File.GetLastWriteTime(path).ToString("HH:mm") + ")" }, tick);
                    shots.SelectedIndex = shots.Items.Count - 1;
                };
                foreach (string r in Report.RecentScreenshots(current, TimeSpan.FromHours(2))) addShot(r, false, L.T("최근: ", "recent: "));
                shots.SelectedIndexChanged += (s, e) =>
                {
                    var old = preview.Image;
                    preview.Image = null;
                    if (old != null) old.Dispose();
                    var sel = shots.SelectedItem as Shot;
                    if (sel == null) return;
                    try { using (var img = Image.FromFile(sel.Path)) preview.Image = new Bitmap(img); } catch { }
                };
                int pasted = 0;
                Action paste = () =>
                {
                    if (Clipboard.ContainsImage())
                    {
                        string p = Path.Combine(Path.GetTempPath(), "Flato3D_paste_" + DateTime.Now.ToString("HHmmss") + "_" + (++pasted) + ".png");
                        using (var img = Clipboard.GetImage()) img.Save(p, System.Drawing.Imaging.ImageFormat.Png);
                        addShot(p, true, L.T("붙여넣음: ", "pasted: "));
                    }
                    else if (Clipboard.ContainsFileDropList())
                        foreach (string p in Clipboard.GetFileDropList())
                            if (Regex.IsMatch(Path.GetExtension(p), @"^\.(png|jpe?g|bmp)$", RegexOptions.IgnoreCase)) addShot(p, true, "");
                };
                var shotButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
                var addFile = new Button { Text = L.T("파일 추가...", "Add files..."), AutoSize = true, MinimumSize = new Size(P(90), P(28)) };
                var pasteBtn = new Button { Text = L.T("붙여넣기 (Ctrl+V)", "Paste (Ctrl+V)"), AutoSize = true, MinimumSize = new Size(P(90), P(28)) };
                var removeShot = new Button { Text = L.T("빼기", "Remove"), AutoSize = true, MinimumSize = new Size(P(90), P(28)) };
                addFile.Click += (s, e) =>
                {
                    using (var d = new OpenFileDialog { Multiselect = true, Filter = L.T("이미지", "Images") + "|*.png;*.jpg;*.jpeg;*.bmp" })
                        if (d.ShowDialog(f) == DialogResult.OK) foreach (string p in d.FileNames) addShot(p, true, "");
                };
                pasteBtn.Click += (s, e) => paste();
                removeShot.Click += (s, e) => { if (shots.SelectedIndex >= 0) shots.Items.RemoveAt(shots.SelectedIndex); };
                shotButtons.Controls.AddRange(new Control[] { addFile, pasteBtn, removeShot });
                f.KeyPreview = true;
                f.KeyDown += (s, e) =>   // Ctrl+V with an image (not text) on the clipboard adds a screenshot, wherever the focus is
                {
                    if (e.Control && e.KeyCode == Keys.V && (Clipboard.ContainsImage() || Clipboard.ContainsFileDropList())) { paste(); e.SuppressKeyPress = true; }
                };
                f.FormClosed += (s, e) => { if (preview.Image != null) preview.Image.Dispose(); };
                // the result first: works / some problems / does not work
                var resultRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
                resultRow.Controls.Add(new Label { Text = L.T("결과:", "Result:"), AutoSize = true, Padding = new Padding(0, P(4), P(6), 0) });
                var radios = Report.Results.Select(r => new RadioButton { Text = Report.ResultLabel(r), Tag = r, AutoSize = true, Margin = new Padding(P(4), 0, P(14), 0) }).ToList();
                resultRow.Controls.AddRange(radios.ToArray());
                var text = new Label { AutoSize = true, MaximumSize = new Size(P(740), 0), Text = L.T(
                    "메모: 문제가 있으면 증상을 적습니다(예: 캐릭터가 배경 뒤로 들어감, 시작하면 멈춤, 화면이 검음). 잘 되면 비워 두거나 소감을 적습니다.\r\n" +
                    "게임을 실행해 그 장면에서 Ctrl+F12를 누른 뒤 보고하면 그때의 로그와 두 눈 이미지가 들어갑니다. 로그와 설정은 자동으로 모읍니다.\r\n저장 위치: ",
                    "Note: describe the problem if there is one (e.g. a character goes behind the background, the game hangs at start, black screen); leave it empty or add a remark if it works.\r\n" +
                    "Run the game, press Ctrl+F12 at that scene, then report: that run's log and both eyes are included. Logs and settings are collected automatically.\r\nSaved in: ") +
                    folder + (pre ? L.T(" (OneDrive pre-release)", " (OneDrive pre-release)") : "") };
                var box = new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, AcceptsReturn = true };
                var gh = new CheckBox { AutoSize = true, Checked = false, Text = L.T("GitHub에도 보고 (공개 페이지가 열리고, 만든 zip을 거기에 끌어다 놓음)", "Also report on GitHub (opens a public page; drag the zip there)") };
                foreach (var rb in radios) rb.CheckedChanged += (s, e) => { if (((RadioButton)s).Checked) gh.Checked = !pre && (string)((RadioButton)s).Tag != "works"; };
                var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
                var ok = new Button { Text = L.T("저장", "Save"), DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(P(90), P(28)) };
                var cancel = new Button { Text = L.T("취소", "Cancel"), DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(P(90), P(28)) };
                ok.Click += (s, e) =>
                {
                    if (radios.Any(r => r.Checked)) return;
                    MessageBox.Show(f, L.T("결과(잘 됨 / 일부 문제 / 안 됨)를 고르세요.", "Choose a result (works / some problems / does not work)."), f.Text);
                    f.DialogResult = DialogResult.None;
                };
                buttons.Controls.AddRange(new Control[] { cancel, ok });
                layout.Controls.Add(resultRow, 0, 0);
                layout.Controls.Add(text, 0, 1);
                layout.Controls.Add(box, 0, 2);
                layout.Controls.Add(new Label { AutoSize = true, Padding = new Padding(0, P(8), 0, P(2)), Text = L.T(
                    "스크린샷: 체크한 것만 들어갑니다. Win+Shift+S로 캡처한 뒤 Ctrl+V로 붙여 넣을 수 있습니다. 최근 캡처(2시간)는 미리 목록에 있습니다.",
                    "Screenshots: only ticked ones are included. Capture with Win+Shift+S and paste with Ctrl+V; recent captures (2 hours) are listed.") }, 0, 3);
                layout.Controls.Add(shotArea, 0, 4);
                layout.Controls.Add(shotButtons, 0, 5);
                layout.Controls.Add(gh, 0, 6);
                layout.Controls.Add(buttons, 0, 7);
                f.Controls.Add(layout);
                f.CancelButton = cancel;
                Theme.TitleBar(f);
                Theme.Primary(ok);
                Theme.Apply(f);
                if (f.ShowDialog(this) != DialogResult.OK) return;
                string result = (string)radios.First(r => r.Checked).Tag;
                try
                {
                    string summary;
                    var images = shots.CheckedItems.Cast<Shot>().Select(x => x.Path).ToList();
                    string gameDir = currentExe == null ? null : Path.GetDirectoryName(currentExe);
                    string dir = Report.Create(current, currentExe, result, box.Text, images, out summary);
                    foreach (string p in images.Where(p => Path.GetFileName(p).StartsWith("Flato3D_paste_"))) { try { File.Delete(p); } catch { } }
                    if (gameDir != null) Setup.RecordUserStatus(gameDir, result, box.Text.Trim());
                    var msg = new StringBuilder(L.T("상태 보고를 저장했습니다 (", "Status report saved (") + Report.ResultLabel(result) + "): " + dir + "\r\n");
                    if (pre) msg.AppendLine(L.T("OneDrive pre-release 폴더입니다. 이 보고를 기준으로 상태를 반영하고 문제를 고칩니다.", "This is the OneDrive pre-release folder; status updates and fixes start from here."));
                    if (gh.Checked) msg.AppendLine(L.T("GitHub 보고용 zip: ", "zip for the GitHub issue: ") + Report.OpenIssue(dir, current, result, summary));
                    logBox.Text = msg.ToString();
                }
                catch (Exception e) { logBox.Text = L.T("상태 보고 저장 실패: ", "Report failed: ") + e.Message; }
                RefreshGame(current);
            }
        }

        void DoUninstall()
        {
            if (current == null || currentExe == null) return;
            if (MessageBox.Show(this, L.T("Flato3D를 이 게임에서 제거하고 원래 파일을 되돌릴까요?", "Remove Flato3D from this game and restore the original files?"), Text, MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            string dir = Path.GetDirectoryName(currentExe);
            var exclusions = Setup.DefenderExclusions(dir);
            try { logBox.Text = Setup.Uninstall(dir).Replace("\n", "\r\n"); }
            catch (Exception e) { logBox.Text = L.T("제거 실패: ", "Uninstall failed: ") + e.Message; }
            if (exclusions.Count > 0 &&
                MessageBox.Show(this, L.T("이 게임에 등록한 Defender 예외도 지울까요?\r\n", "Also remove the Defender exclusion added for this game?\r\n") + string.Join("\r\n", exclusions),
                                Text, MessageBoxButtons.YesNo) == DialogResult.Yes)
                foreach (string p in exclusions)
                    logBox.AppendText("\r\n" + (Defender.Exclude(p, false) ? L.T("Defender 예외 지움: ", "Defender exclusion removed: ") : L.T("Defender 예외를 지우지 못함: ", "Could not remove the Defender exclusion: ")) + p);
            RefreshGame(current);
        }

        void DoDefender()
        {
            if (current == null || currentExe == null) return;
            string dir = Path.GetDirectoryName(currentExe), dll = Path.Combine(dir, "version.dll");
            if (MessageBox.Show(this, Defender.Notice() + "\r\n\r\n" +
                                L.T("다음 파일을 Windows Defender 예외로 등록할까요? 관리자 권한 확인 창이 뜹니다.\r\n", "Add this file to the Windows Defender exclusions? Windows will ask for permission.\r\n") + dll,
                                Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;
            if (Defender.Exclude(dll, true))
            {
                Setup.RecordDefender(dir, dll, true);
                logBox.Text = L.T("Defender 예외 등록: ", "Defender exclusion added: ") + dll + "\r\n" +
                              (Defender.Readable(dll) ? "" : L.T("version.dll이 이미 격리되었습니다. [설치]를 다시 누르세요.", "version.dll was already quarantined: press [Install] again."));
            }
            else
                logBox.Text = L.T("Defender 예외를 등록하지 못했습니다. 권한 확인을 취소했거나 다른 백신을 쓰는 경우입니다. 그 백신에서 이 파일을 예외로 등록하세요: ",
                                  "Could not add the Defender exclusion: the permission prompt was declined, or another antivirus is active. Exclude this file there: ") + dll;
            RefreshGame(current);
        }

        void DoRun()
        {
            if (current == null || currentExe == null) return;
            try
            {
                if (current.SteamAppId != null) Process.Start("steam://rungameid/" + current.SteamAppId);
                else Process.Start(new ProcessStartInfo(currentExe) { WorkingDirectory = Path.GetDirectoryName(currentExe) });
            }
            catch (Exception e) { logBox.Text = e.Message; }
        }

        // --screenshot: fills the list now (Shown does that in normal use) and selects the given game
        public void SelectForScreenshot(string game)
        {
            Rescan();
            foreach (ListViewItem it in games.Items)
                if (game != null && it.Text.IndexOf(game, StringComparison.OrdinalIgnoreCase) >= 0) { it.Selected = true; it.EnsureVisible(); break; }
        }

        // The status column: the user's own report for a game installed here (until it reaches the profile),
        // else what the profile / candidate list says.
        public static string StatusFor(Game g, string exe, out Color? color)
        {
            var us = Setup.UserStatus(exe == null ? null : Path.GetDirectoryName(exe));
            string r = Json.Str(us, "result");
            if (r == null) { color = null; return Library.StatusText(g); }
            color = r == "works" ? Theme.Works : r == "issues" ? Theme.Issues : Theme.Broken;
            return Report.ResultLabel(r) + L.T(" (내 확인 ", " (my check ") + Json.Str(us, "date") + ")";
        }

        void RefreshGame(Game g)
        {
            string exe = currentExe;
            foreach (ListViewItem it in games.Items)
                if (it.Tag == g)
                {
                    Color? mine;
                    it.SubItems[2].Text = StatusFor(g, exe, out mine);
                    it.SubItems[3].Text = Setup.Status(Path.GetDirectoryName(exe));
                    if (mine != null) it.ForeColor = mine.Value;
                }
            ShowGame(g, exe);
        }
    }
}
