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
            if (args.Length > 0) return Cli(args);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
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
                        log.AppendLine(string.Join(" | ", g.Title, g.Store, Library.StatusText(g), exe == null ? "-" : Pe.Arch(exe) ?? "?", r, why,
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
                }
                else if (a[0] == "--install")
                {
                    var o = new Options();
                    o.Output = Arg(a, "--output") ?? Json.Str(Json.Obj(Json.Obj(game.Profile, "ini"), "Stereo"), "output") ?? "auto";
                    string sep = Arg(a, "--separation") ?? Json.Str(Json.Obj(Json.Obj(game.Profile, "ini"), "Stereo"), "separation");
                    if (sep != null) o.Separation = double.Parse(sep, CultureInfo.InvariantCulture);
                    log.Append(Setup.Install(game, exePath, route, o));
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
        readonly CheckBox onlyKnown = new CheckBox();
        readonly TextBox exeBox = new TextBox(), logBox = new TextBox(), candFilter = new TextBox();
        readonly Label info = new Label();
        readonly ComboBox routeBox = new ComboBox(), outputBox = new ComboBox();
        readonly NumericUpDown sepBox = new NumericUpDown();
        readonly Button installBtn = new Button(), removeBtn = new Button(), runBtn = new Button(), folderBtn = new Button(), logBtn = new Button();
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

            var tabs = new TabControl { Dock = DockStyle.Fill };
            var t1 = new TabPage(L.T("게임", "Games"));
            var t2 = new TabPage(L.T("후보 목록", "Candidate list"));
            var t3 = new TabPage(L.T("도움말", "Help"));
            tabs.TabPages.AddRange(new[] { t1, t2, t3 });
            Controls.Add(tabs);
            BuildGamesTab(t1);
            BuildCandidatesTab(t2);
            BuildHelpTab(t3);
            Shown += (s, e) => Rescan();
        }

        void BuildGamesTab(TabPage t)
        {
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(P(6), P(6), P(6), P(2)) };
            onlyKnown.Text = L.T("Flato3D 대상만 보기 (프로필·후보)", "Only Flato3D games (profiles, candidates)");
            onlyKnown.AutoSize = true;
            onlyKnown.Checked = true;
            onlyKnown.CheckedChanged += (s, e) => FillGames();
            var refresh = new Button { Text = L.T("새로 고침", "Refresh"), AutoSize = true };
            refresh.Click += (s, e) => Rescan();
            var browse = new Button { Text = L.T("exe 직접 고르기...", "Choose an exe..."), AutoSize = true };
            browse.Click += (s, e) => BrowseExe();
            top.Controls.AddRange(new Control[] { onlyKnown, refresh, browse });

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

            var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = P(330), ColumnCount = 1, Padding = new Padding(P(6)) };
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
            outputBox.DropDownStyle = ComboBoxStyle.DropDownList;
            outputBox.Width = P(230);
            outputBox.Items.AddRange(new object[] { L.T("SR 디스플레이 (Acer, Samsung)", "SR display (Acer, Samsung)"), L.T("좌우 SBS", "Side by side"), L.T("위아래", "Top and bottom") });
            sepBox.Minimum = 1;
            sepBox.Maximum = 200;
            sepBox.DecimalPlaces = 1;
            sepBox.Width = P(70);
            row2.Controls.AddRange(new Control[] {
                new Label { Text = L.T("경로:", "Route:"), AutoSize = true, Padding = new Padding(0, P(6), 0, 0) }, routeBox,
                new Label { Text = L.T("출력:", "Output:"), AutoSize = true, Padding = new Padding(P(12), P(6), 0, 0) }, outputBox,
                new Label { Text = L.T("분리값(%):", "Separation (%):"), AutoSize = true, Padding = new Padding(P(12), P(6), 0, 0) }, sepBox });

            var row3 = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
            installBtn.Text = L.T("설치", "Install");
            removeBtn.Text = L.T("제거", "Uninstall");
            runBtn.Text = L.T("실행", "Run");
            folderBtn.Text = L.T("폴더 열기", "Open folder");
            logBtn.Text = L.T("로그 보기", "Show log");
            foreach (var b in new[] { installBtn, removeBtn, runBtn, folderBtn, logBtn }) { b.AutoSize = true; b.MinimumSize = new Size(P(90), P(28)); row3.Controls.Add(b); }
            installBtn.Click += (s, e) => DoInstall();
            removeBtn.Click += (s, e) => DoUninstall();
            runBtn.Click += (s, e) => DoRun();
            folderBtn.Click += (s, e) => { if (currentExe != null) Process.Start("explorer.exe", "\"" + Path.GetDirectoryName(currentExe) + "\""); };
            logBtn.Click += (s, e) => { string f = currentExe == null ? null : Path.Combine(Path.GetDirectoryName(currentExe), "OGL3D.log"); if (f != null && File.Exists(f)) Process.Start("notepad.exe", "\"" + f + "\""); };

            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.Dock = DockStyle.Fill;

            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, P(100)));
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

        void BuildCandidatesTab(TabPage t)
        {
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = false, Padding = new Padding(P(6), P(6), P(6), P(2)) };
            candFilter.Width = P(300);
            candFilter.TextChanged += (s, e) => FillCandidates();
            top.Controls.AddRange(new Control[] { new Label { Text = L.T("검색:", "Search:"), AutoSize = true, Padding = new Padding(0, P(4), 0, 0) }, candFilter,
                new Label { Text = L.T("후보 = 평면 2D로 분류된 게임. 경로가 있어도 게임마다 시험이 필요합니다.", "Candidates are games classified as flat 2D. Each still needs testing."), AutoSize = true, Padding = new Padding(P(12), P(4), 0, 0) } });
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

        void BuildHelpTab(TabPage t)
        {
            var box = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, Font = new Font("Segoe UI", 10f) };
            box.Text = string.Join("\r\n", new[] {
                L.T("Flato3D-SR은 평면 2D 게임(픽셀 아트, 손그림)의 레이어와 스프라이트마다 깊이를 줘서 입체로 보여 줍니다.", "Flato3D-SR gives each layer and sprite of a flat 2D game its own depth."),
                "",
                L.T("동작 방식", "How it works"),
                L.T("  OpenGL 게임: opengl32.dll(OGL3D-SR)이 두 눈을 그립니다.", "  OpenGL games: opengl32.dll (OGL3D-SR) renders both eyes."),
                L.T("  Direct3D 8~11 게임: WineD3D가 OpenGL로 바꾸고, version.dll 로더가 게임을 WineD3D로 보냅니다.", "  Direct3D 8-11 games: WineD3D turns them into OpenGL; the version.dll loader sends the game there."),
                L.T("  Direct3D 12, Vulkan, 완성 화면 한 장만 내는 게임(DOSBox, ScummVM, 에뮬레이터)은 할 수 없습니다.", "  Direct3D 12, Vulkan and games that output one finished image (DOSBox, ScummVM, emulators) are not possible."),
                L.T("  다른 도구(geo-11, dgVoodoo, ReShade)의 DLL은 설치할 때 Flato3D_backup 폴더로 옮기고, 제거하면 되돌립니다.", "  Other tools' DLLs (geo-11, dgVoodoo, ReShade) go to Flato3D_backup on install and come back on uninstall."),
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
                string st = Library.StatusText(g);
                string exe = Library.FindExe(g);
                string inst = Setup.Status(exe == null ? null : Path.GetDirectoryName(exe));
                if (onlyKnown.Checked && st == "" && inst == "") continue;
                var it = new ListViewItem(new[] { g.Title, g.Store, st, inst, g.Dir }) { Tag = g };
                if (g.Profile != null) it.ForeColor = Color.DarkGreen;
                else if (g.Candidate != null && new[] { "oneimage", "browser", "unsupported" }.Contains(Json.Str(g.Candidate, "api"))) it.ForeColor = Color.Gray;
                games.Items.Add(it);
            }
            games.EndUpdate();
            info.Text = L.T("게임을 고르세요. 초록 = 프로필이 있는 게임, 회색 = 이 방법으로는 어려운 게임.", "Choose a game. Green = has a profile, gray = hard this way.");
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
            foreach (Control c in new Control[] { installBtn, removeBtn, runBtn, folderBtn, logBtn, routeBox, outputBox, sepBox }) c.Enabled = on;
        }

        void ShowGame(Game g, string exe)
        {
            current = g;
            currentExe = exe ?? Library.FindExe(g);
            exeBox.Text = currentExe ?? L.T("(exe를 찾지 못함: exe 변경으로 고르세요)", "(no exe found: use Change exe)");
            string why;
            string r = Library.DetectRoute(g, currentExe, out why);
            string arch = currentExe == null ? "?" : Pe.Arch(currentExe) ?? "?";
            var stereo = Json.Obj(Json.Obj(g.Profile, "ini"), "Stereo");
            sepBox.Value = (decimal)Math.Max(1, Math.Min(200, double.Parse(Json.Str(stereo, "separation") ?? "25", CultureInfo.InvariantCulture)));
            int oi = Array.IndexOf(Outputs, Json.Str(stereo, "output") ?? "auto");
            outputBox.SelectedIndex = oi < 0 ? 0 : oi;
            int ri = Array.IndexOf(Routes, r);
            routeBox.SelectedIndex = ri < 0 ? 2 : ri;
            var sb = new StringBuilder();
            sb.AppendLine(g.Title + "   [" + Library.StatusText(g) + "]   " + Setup.Status(currentExe == null ? null : Path.GetDirectoryName(currentExe)));
            sb.AppendLine(L.T("판별: ", "Detected: ") + (r == Route.None ? L.T("불가", "not possible") : Route.Label(r)) + " — " + why + "   " + arch);
            foreach (var n in Json.Arr(g.Profile, "known_issues").Take(2)) sb.AppendLine("• " + n);
            if (g.Profile == null && Json.Str(g.Candidate, "note") is string note && note.Length > 0) sb.AppendLine("• " + note);
            foreach (var n in Json.Arr(g.Profile, "remove")) sb.AppendLine(L.T("• 설치 때 옮겨 둠: ", "• moved aside on install: ") + n);
            info.Text = sb.ToString();
            EnableActions(currentExe != null);
            installBtn.Enabled = currentExe != null && r != Route.None;
            removeBtn.Enabled = currentExe != null && File.Exists(Path.Combine(Path.GetDirectoryName(currentExe), Setup.ManifestName));
        }

        void BrowseExe()
        {
            using (var d = new OpenFileDialog { Filter = L.T("실행 파일|*.exe", "Executables|*.exe") })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string dir = Path.GetDirectoryName(d.FileName);
                var g = all.FirstOrDefault(x => d.FileName.StartsWith(x.Dir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
                        ?? new Game { Title = Path.GetFileName(dir), Store = "", Dir = dir };
                if (g.Profile == null && g.Candidate == null) Library.Match(g);
                ShowGame(g, d.FileName);
            }
        }

        void ChangeExe()
        {
            if (current == null) return;
            using (var d = new OpenFileDialog { Filter = L.T("실행 파일|*.exe", "Executables|*.exe"), InitialDirectory = current.Dir })
                if (d.ShowDialog(this) == DialogResult.OK) ShowGame(current, d.FileName);
        }

        void DoInstall()
        {
            if (current == null || currentExe == null) return;
            var o = new Options { Output = Outputs[Math.Max(0, outputBox.SelectedIndex)], Separation = (double)sepBox.Value };
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

        void DoUninstall()
        {
            if (current == null || currentExe == null) return;
            if (MessageBox.Show(this, L.T("Flato3D를 이 게임에서 제거하고 원래 파일을 되돌릴까요?", "Remove Flato3D from this game and restore the original files?"), Text, MessageBoxButtons.OKCancel) != DialogResult.OK) return;
            try { logBox.Text = Setup.Uninstall(Path.GetDirectoryName(currentExe)).Replace("\n", "\r\n"); }
            catch (Exception e) { logBox.Text = L.T("제거 실패: ", "Uninstall failed: ") + e.Message; }
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

        void RefreshGame(Game g)
        {
            string exe = currentExe;
            foreach (ListViewItem it in games.Items)
                if (it.Tag == g) it.SubItems[3].Text = Setup.Status(Path.GetDirectoryName(exe));
            ShowGame(g, exe);
        }
    }
}
