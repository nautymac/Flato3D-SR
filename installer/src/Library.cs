// Installed games (Steam, Heroic), the Flato3D data files (profiles, candidates) and what can be read
// from a game's exe (32/64-bit, imported graphics DLLs) to choose the route.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Flato3D
{
    static class L
    {
        public static readonly bool Ko = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ko";
        public static string T(string ko, string en) { return Ko ? ko : en; }
    }

    class Game
    {
        public string Title, Store, Dir, SteamAppId, HeroicApp, HeroicExe;
        public Dictionary<string, object> Profile, Candidate;
    }

    // Routes: how the runtime reaches the game.
    static class Route
    {
        public const string OpenGL = "opengl", D3D9 = "d3d9", D3D11 = "d3d11", None = "none";
        public static string Label(string r)
        {
            switch (r)
            {
                case OpenGL: return L.T("OpenGL (직접)", "OpenGL (direct)");
                case D3D9: return L.T("Direct3D 8/9 → WineD3D", "Direct3D 8/9 → WineD3D");
                case D3D11: return L.T("Direct3D 10/11 → WineD3D", "Direct3D 10/11 → WineD3D");
                default: return L.T("불가", "not possible");
            }
        }
    }

    static class Json
    {
        static readonly JavaScriptSerializer S = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
        public static object Load(string path) { return S.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)); }
        public static string Dump(object o) { return S.Serialize(o); }
        public static string Str(Dictionary<string, object> d, string k)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;
        }
        public static Dictionary<string, object> Obj(Dictionary<string, object> d, string k)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) ? v as Dictionary<string, object> : null;
        }
        public static IEnumerable<object> Arr(Dictionary<string, object> d, string k)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) && v is object[] ? (object[])v : new object[0];
        }
    }

    // The runtime files and data. In the repository they sit in the build folders; in a release
    // package the same relative paths without bin\.
    static class Payload
    {
        public static string Root;
        public static List<Dictionary<string, object>> Profiles = new List<Dictionary<string, object>>();
        public static List<Dictionary<string, object>> Candidates = new List<Dictionary<string, object>>();

        public static bool Find()
        {
            string d = AppDomain.CurrentDomain.BaseDirectory;
            for (int i = 0; i < 4 && d != null; ++i, d = Path.GetDirectoryName(d.TrimEnd('\\')))
                if (File.Exists(Path.Combine(d, "data", "game_profiles.json"))) { Root = d; break; }
            if (Root == null) return false;
            var p = Json.Load(Path.Combine(Root, "data", "game_profiles.json")) as Dictionary<string, object>;
            foreach (var g in Json.Arr(p, "games")) Profiles.Add((Dictionary<string, object>)g);
            string cand = Path.Combine(Root, "data", "game_candidates.json");
            if (File.Exists(cand))
                foreach (var g in Json.Arr(Json.Load(cand) as Dictionary<string, object>, "games")) Candidates.Add((Dictionary<string, object>)g);
            return true;
        }

        static string First(params string[] rel)
        {
            foreach (string r in rel)
            {
                string f = Path.Combine(Root, r);
                if (File.Exists(f) || Directory.Exists(f)) return f;
            }
            return null;
        }
        public static string OpenGl32(string arch) { return First(@"ogl3d\bin\" + arch + @"\opengl32.dll", @"ogl3d\" + arch + @"\opengl32.dll"); }
        public static string Loader(string arch) { return First(@"loader\bin\" + arch + @"\version.dll", @"loader\" + arch + @"\version.dll"); }
        public static string IniTemplate() { return First(@"ogl3d\OGL3D.ini"); }
        public static string WineD3D(string arch, bool renamed)
        {
            return First(@"third_party\wined3d-11.19\" + arch + (renamed ? "-renamed" : ""), @"wined3d\" + arch + (renamed ? "-renamed" : ""));
        }
    }

    static class Library
    {
        public static string Norm(string s) { return s == null ? "" : Regex.Replace(s.ToLowerInvariant(), "[^a-z0-9]", ""); }

        public static List<Game> Scan()
        {
            var games = new List<Game>();
            try { ScanSteam(games); } catch { }
            try { ScanHeroic(games); } catch { }
            foreach (var g in games) Match(g);
            return games.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        public static string SteamPath()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
            {
                string p = k == null ? null : k.GetValue("SteamPath") as string;
                return p == null ? null : p.Replace('/', '\\');
            }
        }

        static void ScanSteam(List<Game> games)
        {
            string steam = SteamPath();
            if (steam == null) return;
            var libs = new List<string> { steam };
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string lib in libs.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                string apps = Path.Combine(lib, "steamapps");
                if (!Directory.Exists(apps)) continue;
                foreach (string acf in Directory.GetFiles(apps, "appmanifest_*.acf"))
                {
                    string t = File.ReadAllText(acf);
                    string id = Field(t, "appid"), name = Field(t, "name"), dir = Field(t, "installdir");
                    if (id == null || dir == null || id == "228980" || Regex.IsMatch(name ?? "", "Proton|Steamworks|Steam Linux Runtime|SteamVR|Redistributable")) continue;
                    string full = Path.Combine(apps, "common", dir);
                    if (!Directory.Exists(full) || !seen.Add(full)) continue;
                    games.Add(new Game { Title = name ?? dir, Store = "Steam", Dir = full, SteamAppId = id });
                }
            }
        }

        static string Field(string acf, string key)
        {
            var m = Regex.Match(acf, "\"" + key + "\"\\s+\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        static void ScanHeroic(List<Game> games)
        {
            string h = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "heroic");
            string epic = Path.Combine(h, "legendaryConfig", "legendary", "installed.json");
            if (File.Exists(epic))
                foreach (var kv in (Dictionary<string, object>)Json.Load(epic))
                {
                    var e = kv.Value as Dictionary<string, object>;
                    string dir = Json.Str(e, "install_path");
                    if (dir == null || !Directory.Exists(dir) || Json.Str(e, "is_dlc") == "True") continue;
                    games.Add(new Game { Title = Json.Str(e, "title") ?? Path.GetFileName(dir), Store = "Epic", Dir = dir, HeroicApp = kv.Key, HeroicExe = Json.Str(e, "executable") });
                }
            string gog = Path.Combine(h, "gog_store", "installed.json");
            if (File.Exists(gog))
                foreach (var o in Json.Arr((Dictionary<string, object>)Json.Load(gog), "installed"))
                {
                    var e = (Dictionary<string, object>)o;
                    string dir = Json.Str(e, "install_path");
                    if (dir == null || !Directory.Exists(dir) || Json.Str(e, "is_dlc") == "True") continue;
                    games.Add(new Game { Title = Path.GetFileName(dir.TrimEnd('\\')), Store = "GOG", Dir = dir, HeroicApp = Json.Str(e, "appName"), HeroicExe = Json.Str(e, "executable") });
                }
        }

        public static void Match(Game g)
        {
            string folder = Path.GetFileName(g.Dir.TrimEnd('\\'));
            g.Profile = Payload.Profiles.FirstOrDefault(p =>
                (g.SteamAppId != null && Json.Str(p, "steam_appid") == g.SteamAppId) ||
                string.Equals(Json.Str(p, "folder"), folder, StringComparison.OrdinalIgnoreCase));
            string nt = Norm(g.Title), nf = Norm(folder);
            g.Candidate = Payload.Candidates.FirstOrDefault(c =>
                (g.SteamAppId != null && Json.Str(c, "steam_appid") == g.SteamAppId) ||
                (Norm(Json.Str(c, "title")) is string ct && ct.Length > 0 && (ct == nt || ct == nf)));
        }

        // The game's main executable: the profile's, the Heroic record's, an Unreal shipping exe, else
        // the exe whose name matches the game best (the largest one among equals).
        static readonly string[] NotGame = { "unins", "crash", "setup", "launcher", "redist", "vcredist", "dxsetup", "dotnet", "update", "helper", "report", "unitycrashhandler", "ue4prereq", "easyanticheat", "cefsharp", "nw.exe" };

        public static string FindExe(Game g)
        {
            string pexe = Json.Str(g.Profile, "exe");
            if (pexe != null) pexe = pexe.Replace('/', '\\');
            if (pexe != null && File.Exists(Path.Combine(g.Dir, pexe))) return Path.Combine(g.Dir, pexe);
            if (!string.IsNullOrEmpty(g.HeroicExe) && File.Exists(Path.Combine(g.Dir, g.HeroicExe))) return Path.Combine(g.Dir, g.HeroicExe);
            var ship = SafeFiles(g.Dir, "*-Shipping.exe", SearchOption.AllDirectories).FirstOrDefault(f => f.IndexOf(@"\Binaries\Win", StringComparison.OrdinalIgnoreCase) >= 0);
            if (ship != null) return ship;
            var exes = new List<string>(SafeFiles(g.Dir, "*.exe", SearchOption.TopDirectoryOnly));
            foreach (string sub in new[] { "bin", "Bin", "x64", "bin64", "Binaries", "game", "Game" })
                if (Directory.Exists(Path.Combine(g.Dir, sub))) exes.AddRange(SafeFiles(Path.Combine(g.Dir, sub), "*.exe", SearchOption.TopDirectoryOnly));
            exes = exes.Where(e => !NotGame.Any(n => Path.GetFileName(e).ToLowerInvariant().Contains(n))).ToList();
            if (exes.Count == 0) return null;
            string nt = Norm(g.Title), nf = Norm(Path.GetFileName(g.Dir.TrimEnd('\\')));
            return exes.OrderByDescending(e => { string n = Norm(Path.GetFileNameWithoutExtension(e)); return n == nt || n == nf ? 2 : (nt.Contains(n) || nf.Contains(n)) && n.Length > 2 ? 1 : 0; })
                       .ThenByDescending(e => new FileInfo(e).Length).First();
        }

        static IEnumerable<string> SafeFiles(string dir, string pattern, SearchOption opt)
        {
            try { return Directory.GetFiles(dir, pattern, opt); } catch { return new string[0]; }
        }

        // Route from the profile, the exe's imports, the engine files next to it, then the candidate list.
        public static string DetectRoute(Game g, string exe, out string why)
        {
            string pr = Json.Str(g.Profile, "route"), papi = Json.Str(g.Profile, "api") ?? "";
            if (pr == "opengl") { why = L.T("프로필", "profile"); return Route.OpenGL; }
            if (pr == "wined3d") { why = L.T("프로필", "profile"); return papi.Contains("9") || papi.Contains(" 8") ? Route.D3D9 : Route.D3D11; }
            if (exe != null && File.Exists(exe))
            {
                var imp = Pe.Imports(exe);
                if (imp.Contains("d3d12.dll") || imp.Contains("vulkan-1.dll")) { why = L.T("exe가 D3D12/Vulkan을 가져옴", "the exe imports D3D12/Vulkan"); return Route.None; }
                if (imp.Contains("d3d11.dll") || imp.Contains("dxgi.dll") || imp.Contains("d3d10.dll") || imp.Contains("d3d10_1.dll")) { why = L.T("exe가 D3D10/11을 가져옴", "the exe imports D3D10/11"); return Route.D3D11; }
                if (imp.Contains("d3d9.dll") || imp.Contains("d3d8.dll") || imp.Contains("ddraw.dll")) { why = L.T("exe가 D3D8/9를 가져옴", "the exe imports D3D8/9"); return Route.D3D9; }
                if (imp.Contains("opengl32.dll")) { why = L.T("exe가 OpenGL을 가져옴", "the exe imports OpenGL"); return Route.OpenGL; }
                string dir = Path.GetDirectoryName(exe);
                if (File.Exists(Path.Combine(dir, "UnityPlayer.dll"))) { why = L.T("Unity (기본 D3D11)", "Unity (D3D11 by default)"); return Route.D3D11; }
                if (exe.EndsWith("-Shipping.exe", StringComparison.OrdinalIgnoreCase)) { why = L.T("Unreal Engine (기본 D3D11)", "Unreal Engine (D3D11 by default)"); return Route.D3D11; }
            }
            switch (Json.Str(g.Candidate, "api"))
            {
                case "opengl": why = L.T("후보 목록", "candidate list"); return Route.OpenGL;
                case "opengl_alt": why = L.T("후보 목록 (게임을 OpenGL로 바꿔야 함)", "candidate list (switch the game to OpenGL)"); return Route.OpenGL;
                case "d3d9": why = L.T("후보 목록", "candidate list"); return Route.D3D9;
                case "d3d11": why = L.T("후보 목록", "candidate list"); return Route.D3D11;
                case "oneimage": why = L.T("완성 화면 한 장만 나옴 (AI 깊이만 가능)", "one finished image (AI depth only)"); return Route.None;
                case "browser": why = L.T("브라우저 엔진", "browser engine"); return Route.None;
                case "unsupported": why = "D3D12/Vulkan"; return Route.None;
            }
            why = L.T("판별 못함: 직접 고르세요", "unknown: choose one");
            return Route.D3D11;
        }

        // What the list shows for a game.
        public static string StatusText(Game g)
        {
            string st = Json.Str(g.Profile, "status");
            if (st == "verified") return L.T("확인됨", "verified");
            if (st != null) return L.T("동작 (프로필)", "works (profile)");
            if (Json.Str(g.Candidate, "kind") == "1") return L.T("2.5D (일반 입체)", "2.5D (plain stereo)");
            switch (Json.Str(g.Candidate, "api"))
            {
                case null: return "";
                case "oneimage": return L.T("AI 깊이만", "AI depth only");
                case "browser": return L.T("어려움 (브라우저)", "hard (browser)");
                case "unsupported": return L.T("불가 (D3D12/Vulkan)", "no (D3D12/Vulkan)");
                case "unknown": return L.T("후보 (API 미확인)", "candidate (API unknown)");
                default: return L.T("후보 (시험 전)", "candidate (untested)");
            }
        }
    }

    // What the installer pre-selects for a game, with the reason shown for each choice.
    class Recommendation
    {
        public string Output = "auto", Rule = "order";
        public bool Upscale, Flat = true;
        public double Separation = 25;
        public string OutputWhy = "", UpscaleWhy = "", RuleWhy = "";
    }

    static class Recommend
    {
        static readonly string[] SrRuntimeDirs = {
            @"C:\Program Files\Acer\SpatialLabs\Platform\bin", @"C:\Program Files\Simulated Reality\Platform\bin", @"C:\Program Files\LeiaSR\Platform\bin" };

        // An SR display's runtime is installed (Acer SpatialLabs, Samsung Odyssey 3D, Leia SR).
        public static bool SrRuntime()
        {
            return SrRuntimeDirs.Any(d => File.Exists(Path.Combine(d, "SimulatedRealityCore.dll")));
        }

        // Profile first, then the candidate list (PCGamingWiki), the exe and this PC.
        public static Recommendation For(Game g, string exe)
        {
            var r = new Recommendation();
            var ini = Json.Obj(g.Profile, "ini");
            Dictionary<string, object> stereo = Json.Obj(ini, "Stereo"), flat = Json.Obj(ini, "Flat"), disp = Json.Obj(ini, "Display");
            string profile = L.T("프로필", "profile");

            string sep = Json.Str(stereo, "separation");
            if (sep != null) r.Separation = double.Parse(sep, System.Globalization.CultureInfo.InvariantCulture);

            // depth: 2.5D games get plain stereo, adventure games stand sprites on their feet, the rest go by drawing order
            if (Json.Str(flat, "enable") == "0" || (flat == null && Json.Str(g.Candidate, "kind") == "1"))
            {
                r.Flat = false;
                r.RuleWhy = flat != null ? profile : L.T("2.5D 게임 (후보 목록)", "2.5D game (candidate list)");
            }
            else if (Json.Str(flat, "rule") != null)
            {
                r.Rule = Json.Str(flat, "rule");
                r.RuleWhy = profile;
            }
            else
            {
                r.Rule = Json.Str(g.Candidate, "depth_rule") == "feet_y" ? "feet" : "order";
                string genre = Json.Str(g.Candidate, "genre");
                r.RuleWhy = g.Candidate == null ? L.T("기본값", "default") : L.T("후보 목록", "candidate list") + (genre != null ? ": " + genre : "");
            }

            // output: an SR display when its runtime is installed, else side by side
            string o = Json.Str(stereo, "output");
            if (o == "sbs" || o == "tab") { r.Output = o; r.OutputWhy = profile; }
            else if (SrRuntime()) { r.Output = "auto"; r.OutputWhy = L.T("SR 런타임 있음", "SR runtime found"); }
            else { r.Output = "sbs"; r.OutputWhy = L.T("SR 런타임 없음", "no SR runtime"); }

            // upscale: when the game cannot render at 4K (or its UI stays tiny there), unless Windows cannot scale it
            string pu = Json.Str(disp, "upscale"), cu = Json.Str(g.Candidate, "upscale");
            string uhd = Json.Str(g.Candidate, "uhd"), note = Json.Str(g.Candidate, "uhd_note") ?? "";
            string basis = "PCGamingWiki 4K: " + uhd + (note.Length > 0 ? " — " + (note.Length > 80 ? note.Substring(0, 80) + "..." : note) : "");
            if (pu != null) { r.Upscale = pu == "1"; r.UpscaleWhy = profile; }
            else if (cu == "1" && exe != null && Pe.DpiAware(exe))
                r.UpscaleWhy = L.T("4K가 맞지 않지만 exe가 고해상도 인식(DPI aware)이라 업스케일이 듣지 않음", "4K does not fit, but the exe is DPI aware: upscale has no effect") + " (" + basis + ")";
            else if (cu == "1") { r.Upscale = true; r.UpscaleWhy = basis; }
            else if (cu == "0") r.UpscaleWhy = basis;
            else r.UpscaleWhy = L.T("4K 정보 없음", "no 4K information");
            return r;
        }
    }

    static class Pe
    {
        // The exe's manifest declares DPI awareness: Windows never scales it, so upscale mode cannot
        // make it render at a lower resolution.
        public static bool DpiAware(string exe)
        {
            try
            {
                if (new FileInfo(exe).Length > 256L << 20) return false;
                string s = Encoding.GetEncoding(28591).GetString(File.ReadAllBytes(exe));
                return Regex.IsMatch(s, @"<(?:\w+:)?dpiAware(?:ness)?\b[^>]*>\s*(?:true|per ?monitor)", RegexOptions.IgnoreCase);
            }
            catch { return false; }
        }

        public static string Arch(string exe)
        {
            try
            {
                using (var r = new BinaryReader(File.OpenRead(exe)))
                {
                    r.BaseStream.Seek(0x3c, SeekOrigin.Begin);
                    r.BaseStream.Seek(r.ReadInt32() + 4, SeekOrigin.Begin);
                    ushort m = r.ReadUInt16();
                    return m == 0x8664 ? "x64" : m == 0x14c ? "x86" : null;
                }
            }
            catch { return null; }
        }

        // Names of the DLLs in the import and delay-load tables, lower case.
        public static HashSet<string> Imports(string exe)
        {
            var set = new HashSet<string>();
            try
            {
                byte[] d = File.ReadAllBytes(exe);
                int pe = BitConverter.ToInt32(d, 0x3c);
                int nsec = BitConverter.ToUInt16(d, pe + 6), optsz = BitConverter.ToUInt16(d, pe + 20), opt = pe + 24;
                bool x64 = BitConverter.ToUInt16(d, opt) == 0x20b;
                int dirs = opt + (x64 ? 112 : 96);
                Func<int, int> off = rva =>
                {
                    for (int i = 0; i < nsec; ++i)
                    {
                        int s = opt + optsz + i * 40, vs = BitConverter.ToInt32(d, s + 8), va = BitConverter.ToInt32(d, s + 12), rs = BitConverter.ToInt32(d, s + 16), ro = BitConverter.ToInt32(d, s + 20);
                        if (rva >= va && rva < va + Math.Max(vs, rs)) return rva - va + ro;
                    }
                    return -1;
                };
                Func<int, string> name = rva =>
                {
                    int o = off(rva);
                    if (o < 0) return null;
                    int e = o;
                    while (e < d.Length && d[e] != 0) ++e;
                    return Encoding.ASCII.GetString(d, o, e - o).ToLowerInvariant();
                };
                for (int which = 0; which < 2; ++which)
                {
                    int rva = BitConverter.ToInt32(d, dirs + (which == 0 ? 8 : 13 * 8));   // import, delay import
                    int o = rva == 0 ? -1 : off(rva);
                    for (; o >= 0 && o + 32 <= d.Length; o += which == 0 ? 20 : 32)
                    {
                        int nrva = BitConverter.ToInt32(d, o + (which == 0 ? 12 : 4));
                        if (nrva == 0) break;
                        string n = name(nrva);
                        if (n != null) set.Add(n);
                    }
                }
            }
            catch { }
            return set;
        }

        public static bool HasAscii(string file, string text)
        {
            try
            {
                byte[] d = File.ReadAllBytes(file), t = Encoding.ASCII.GetBytes(text);
                for (int i = 0; i + t.Length <= d.Length; ++i)
                {
                    int j = 0;
                    while (j < t.Length && d[i + j] == t[j]) ++j;
                    if (j == t.Length) return true;
                }
            }
            catch { }
            return false;
        }
    }
}
