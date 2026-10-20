// Status reports ("상태 보고": works / some problems / does not work): what is needed to record a game's
// state or look into its problem later, in one folder.
// On the developer's PCs the folder goes into the OneDrive pre-release folder
// (<OneDrive>\AI\ClaudeCode\Flato3D_github_release\reports), where problems are picked up and fixed;
// elsewhere into %LOCALAPPDATA%\Flato3D\reports. The report is also filed as a GitHub issue (label "report"),
// from which the public status list STATUS.md is rebuilt.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Flato3D
{
    static class Report
    {
        public const string IssuesUrl = "https://github.com/nautymac/Flato3D-SR/issues/new";
        const long LogTail = 8L << 20;   // the end of a long trace log is what matters

        // reports folder; preRelease: the developer's OneDrive pre-release folder
        public static string Folder(out bool preRelease)
        {
            foreach (string od in new[] { Environment.GetEnvironmentVariable("OneDriveConsumer"), Environment.GetEnvironmentVariable("OneDrive") })
            {
                if (string.IsNullOrEmpty(od)) continue;
                string rel = Path.Combine(od, @"AI\ClaudeCode\Flato3D_github_release");
                if (Directory.Exists(rel)) { preRelease = true; return Path.Combine(rel, "reports"); }
            }
            preRelease = false;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flato3D", "reports");
        }

        // Screenshots taken lately that may show the problem: the game's Steam screenshots, Windows and Game Bar captures.
        public static List<string> RecentScreenshots(Game g, TimeSpan age)
        {
            var dirs = new List<string>();
            string steam = Library.SteamPath(), ud = steam == null ? null : Path.Combine(steam, "userdata");
            if (ud != null && g.SteamAppId != null && Directory.Exists(ud))
                foreach (string u in Directory.GetDirectories(ud)) dirs.Add(Path.Combine(u, "760", "remote", g.SteamAppId, "screenshots"));
            foreach (string pics in new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                                            Path.Combine(Environment.GetEnvironmentVariable("OneDrive") ?? "", "Pictures"),
                                            Path.Combine(Environment.GetEnvironmentVariable("OneDrive") ?? "", "사진") })
            {
                dirs.Add(Path.Combine(pics, "Screenshots"));
                dirs.Add(Path.Combine(pics, "스크린샷"));
            }
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "Captures"));
            DateTime since = DateTime.Now - age;
            var found = new List<string>();
            foreach (string d in dirs.Where(d => d.Length > 3).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (!Directory.Exists(d)) continue;
                    found.AddRange(Directory.GetFiles(d).Where(f => Regex.IsMatch(Path.GetExtension(f), @"^\.(png|jpe?g|bmp)$", RegexOptions.IgnoreCase) &&
                                                                    File.GetLastWriteTime(f) > since));
                }
                catch { }
            }
            return found.OrderByDescending(File.GetLastWriteTime).Take(20).ToList();
        }

        public static readonly string[] Results = { "works", "issues", "broken" };

        public static string ResultLabel(string result)
        {
            switch (result)
            {
                case "works": return L.T("잘 됨", "works");
                case "issues": return L.T("일부 문제", "some problems");
                case "broken": return L.T("안 됨", "does not work");
                default: return result ?? "";
            }
        }

        // Writes the report folder (report.txt, the game's logs and settings, its latest dump, the chosen
        // screenshots) and returns it. result: works | issues | broken.
        public static string Create(Game g, string exe, string result, string symptom, IEnumerable<string> screenshots, out string summary)
        {
            bool pre;
            string dir = Path.Combine(Folder(out pre), DateTime.Now.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture) + "_" + (result ?? "report") + "_" + Safe(g.Title));
            Directory.CreateDirectory(dir);
            string gameDir = exe == null ? g.Dir : Path.GetDirectoryName(exe);

            var s = new StringBuilder();
            s.AppendLine("Flato3D-SR status report");
            s.AppendLine("time: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            s.AppendLine("result: " + (result ?? "-"));
            s.AppendLine("symptom / note:");
            s.AppendLine(string.IsNullOrWhiteSpace(symptom) ? "(none given)" : symptom.Trim());
            s.AppendLine();
            s.AppendLine("game: " + g.Title + " | store " + (g.Store ?? "-") + " | steam appid " + (g.SteamAppId ?? "-"));
            s.AppendLine("exe: " + (exe ?? "(not found)") + " | " + (exe == null ? "?" : Pe.Arch(exe) ?? "?") + (exe != null && Pe.DpiAware(exe) ? " | DPI aware manifest" : ""));
            string why;
            string route = Library.DetectRoute(g, exe, out why);
            s.AppendLine("route: " + route + " (" + why + ")");
            if (exe != null)
                s.AppendLine("imports: " + string.Join(", ", Pe.Imports(exe).Where(n => Regex.IsMatch(n, "^(d3d|dxgi|opengl|vulkan|ddraw)")).OrderBy(n => n)));
            s.AppendLine("profile: " + (Json.Str(g.Profile, "id") ?? "-") + " (" + (Json.Str(g.Profile, "status") ?? "-") + ")");
            if (g.Candidate != null)
                s.AppendLine("candidate: api " + Json.Str(g.Candidate, "api") + ", kind " + Json.Str(g.Candidate, "kind") + ", genre " + Json.Str(g.Candidate, "genre") +
                             ", engine " + string.Join("/", Json.Arr(g.Candidate, "engine")) + ", 4K " + Json.Str(g.Candidate, "uhd"));
            s.AppendLine("install state: " + Setup.Status(gameDir));
            var rec = Recommend.For(g, exe);
            s.AppendLine("pre-selection: depth " + (rec.Flat ? rec.Rule : "plain stereo") + " (" + rec.RuleWhy + ") | output " + rec.Output + " (" + rec.OutputWhy +
                         ") | upscale " + (rec.Upscale ? 1 : 0) + " (" + rec.UpscaleWhy + ")");
            string ini = gameDir == null ? null : Path.Combine(gameDir, "OGL3D.ini");
            if (ini != null && File.Exists(ini))
            {
                string t = File.ReadAllText(ini, Encoding.UTF8);
                var keys = new[] { "Stereo/separation", "Stereo/convergence", "Stereo/output", "Flat/enable", "Flat/rule", "Flat/depth_top", "Flat/depth_screen",
                                   "Flat/group", "Flat/screen_batch", "Flat/cache_scale", "Flat/silhouette", "Display/upscale", "Display/swap_interval" };
                s.AppendLine("OGL3D.ini: " + string.Join(", ", keys.Select(k => { var p = k.Split('/'); return k + "=" + (Setup.IniGet(t, p[0], p[1]) ?? "-"); })));
            }
            s.AppendLine("installer: " + File.GetLastWriteTime(Process.GetCurrentProcess().MainModule.FileName).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                         " (" + AppDomain.CurrentDomain.BaseDirectory + ")");
            s.AppendLine("windows: " + WindowsVersion());
            s.AppendLine("SR runtime: " + (Recommend.SrRuntime() ? "yes" : "no"));
            s.AppendLine("GPU: " + (Gpu.Known ? string.Join(" | ", Gpu.Names()) : "?") + (Gpu.Hybrid ? " (hybrid)" : ""));
            if (gameDir != null && File.Exists(Path.Combine(gameDir, "version.dll")))
                s.AppendLine("loader version.dll: " + (Defender.Readable(Path.Combine(gameDir, "version.dll")) ? "readable" : "BLOCKED (antivirus?)") +
                             " | Defender exclusions recorded: " + string.Join(", ", Setup.DefenderExclusions(gameDir)));
            int shot = 0;
            foreach (string img in screenshots ?? Enumerable.Empty<string>())
            {
                if (!File.Exists(img)) continue;
                string name = string.Format(CultureInfo.InvariantCulture, "screenshot_{0:00}{1}", ++shot, Path.GetExtension(img).ToLowerInvariant());
                File.Copy(img, Path.Combine(dir, name));
                s.AppendLine("screenshot: " + name + " (" + Path.GetFileName(img) + ", " + File.GetLastWriteTime(img).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ")");
            }
            s.AppendLine();

            // the game's own files next to the exe that matter here
            if (gameDir != null && Directory.Exists(gameDir))
            {
                s.AppendLine("files next to the exe (dll, ini, log, Flato3D):");
                foreach (var f in new DirectoryInfo(gameDir).GetFiles().Where(f => Regex.IsMatch(f.Extension, @"^\.(dll|ini|log)$", RegexOptions.IgnoreCase) ||
                                                                                   f.Name.StartsWith("Flato3D", StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.Name))
                    s.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0,-34} {1,10:N0}  {2:yyyy-MM-dd HH:mm}", f.Name, f.Length, f.LastWriteTime));
                s.AppendLine();
                foreach (var f in new DirectoryInfo(gameDir).GetFiles().Where(f => Regex.IsMatch(f.Name, @"^(OGL3D\.ini|Flato3D_install\.json|Flato3D_loader_.*\.log|OGL3D\.log)$", RegexOptions.IgnoreCase)))
                    File.WriteAllText(Path.Combine(dir, f.Name), Redact(ReadTail(f.FullName)), new UTF8Encoding(false));
                // the latest Ctrl+F12 dump of the day: both eyes of the problem scene
                var dump = new DirectoryInfo(gameDir).GetFiles("OGL3D_frame*.bmp").Where(f => f.LastWriteTime > DateTime.Now.AddDays(-1)).OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                if (dump != null) { File.Copy(dump.FullName, Path.Combine(dir, dump.Name)); s.AppendLine("dump: " + dump.Name); }
                string log = Path.Combine(gameDir, "OGL3D.log");
                if (File.Exists(log))
                {
                    // a log from an earlier day does not show the problem just seen
                    var age = DateTime.Now - File.GetLastWriteTime(log);
                    s.AppendLine("OGL3D.log last written " + File.GetLastWriteTime(log).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) +
                                 (age.TotalHours >= 2 ? string.Format(CultureInfo.InvariantCulture, " ({0:0.#} hours before this report: an older run)", age.TotalHours) : ""));
                    s.Append(LogExcerpt(ReadTail(log)));
                }
                else s.AppendLine("OGL3D.log: none (the wrapper was not loaded, or the game was not started since installing)");
            }
            summary = Redact(s.ToString());
            File.WriteAllText(Path.Combine(dir, "report.txt"), summary, new UTF8Encoding(true));
            return dir;
        }

        // GitHub status list (STATUS.md, rebuilt by a workflow from the issues labelled "report"): with the GitHub CLI
        // signed in, the issue is filed right away; otherwise the status report form opens filled in, and the folder is
        // zipped next to it for the user to attach. Returns the issue address, or the zip.
        public static string FileOnGitHub(string dir, Game g, string result, string note, string summary, out bool filed)
        {
            string title = "[" + g.Title + "] " + ResultLabel(result);
            string first = (note ?? "").Trim().Split('\n')[0].Trim();
            if (result != "works" && first.Length > 0) title += ": " + (first.Length > 60 ? first.Substring(0, 60) + "…" : first);
            string formResult = result == "works" ? "Works well / 잘 됨" : result == "issues" ? "Some problems / 일부 문제" : "Does not work / 안 됨";
            int shots = Directory.GetFiles(dir, "screenshot_*").Length;
            if (GhReady())
            {
                string report = summary.Length > 20000 ? summary.Substring(0, 20000) + "\n...(report.txt in the report folder)" : summary;
                string body = "### Game / 게임\n\n" + g.Title + "\n\n### Steam app id / Steam 앱 번호\n\n" + (g.SteamAppId ?? "_No response_") +
                              "\n\n### Result / 결과\n\n" + formResult + "\n\n### Notes / 메모\n\n" + (string.IsNullOrWhiteSpace(note) ? "_No response_" : note.Trim()) +
                              "\n\n### Report / 보고 내용\n\n```text\n" + report.Replace("```", "'''") + "\n```\n" +
                              (shots > 0 ? "\n" + shots + " screenshot(s) and the logs are in the report folder " + Path.GetFileName(dir) + ".\n" : "");
                string file = Path.Combine(Path.GetTempPath(), "Flato3D_issue_" + Guid.NewGuid().ToString("N") + ".md");
                File.WriteAllText(file, body, new UTF8Encoding(false));
                try
                {
                    string output;
                    int rc = Run(GhPath(), "issue create -R " + Repo + " --title " + Quote(title) + " --label report" + (result == "works" ? "" : " --label bug") +
                                           " --body-file " + Quote(file), out output);
                    var url = Regex.Match(output ?? "", @"https://github\.com/\S+/issues/\d+");
                    if (rc == 0 && url.Success) { filed = true; return url.Value; }
                }
                finally { try { File.Delete(file); } catch { } }
            }
            filed = false;
            string zip = dir + ".zip";
            if (File.Exists(zip)) File.Delete(zip);
            ZipFile.CreateFromDirectory(dir, zip, CompressionLevel.Optimal, false);
            string text = summary.Length > 3000 ? summary.Substring(0, 3000) + "\n...(report.txt in the zip)" : summary;
            string url2 = IssuesUrl + "?template=status_report.yml&labels=report" + (result == "works" ? "" : ",bug") + "&title=" + Uri.EscapeDataString(title) +
                          "&game=" + Uri.EscapeDataString(g.Title) + "&steam=" + Uri.EscapeDataString(g.SteamAppId ?? "") +
                          "&notes=" + Uri.EscapeDataString((note ?? "").Trim()) + "&report=" + Uri.EscapeDataString(text);
            Process.Start(url2);
            Process.Start("explorer.exe", "/select,\"" + zip + "\"");
            return zip;
        }

        const string Repo = "nautymac/Flato3D-SR";
        static bool? ghReady;

        // the GitHub CLI is installed and signed in (the developer's PCs): reports are filed without a browser
        public static bool GhReady()
        {
            if (ghReady == null)
            {
                string output;
                ghReady = GhPath() != null && Run(GhPath(), "auth status", out output) == 0;
            }
            return ghReady.Value;
        }

        static string GhPath()
        {
            foreach (string d in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')
                                 .Concat(new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "GitHub CLI") }))
            {
                try
                {
                    string p = Path.Combine(d.Trim().Trim('"'), "gh.exe");
                    if (d.Trim().Length > 0 && File.Exists(p)) return p;
                }
                catch { }
            }
            return null;
        }

        // one command-line argument (Windows rules: backslashes count only before a quote)
        static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            int slashes = 0;
            foreach (char ch in s ?? "")
            {
                if (ch == '\\') { slashes++; continue; }
                sb.Append('\\', ch == '"' ? slashes * 2 + 1 : slashes);
                sb.Append(ch);
                slashes = 0;
            }
            sb.Append('\\', slashes * 2);
            return sb.Append('"').ToString();
        }

        // runs a console program without a window; both outputs are read as they come (a full pipe would block it)
        static int Run(string exe, string args, out string output)
        {
            var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                                                         RedirectStandardInput = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
            var sb = new StringBuilder();
            using (var p = new Process { StartInfo = psi })
            {
                p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
                p.Start();
                p.StandardInput.Close();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                if (!p.WaitForExit(60000)) { try { p.Kill(); } catch { } output = sb.ToString(); return -1; }
                p.WaitForExit();
                output = sb.ToString();
                return p.ExitCode;
            }
        }

        static string ReadTail(string path)
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (fs.Length > LogTail) fs.Seek(-LogTail, SeekOrigin.End);
                using (var r = new StreamReader(fs, Encoding.UTF8)) return r.ReadToEnd();
            }
        }

        // lines that point at the problem, then the last lines
        static string LogExcerpt(string log)
        {
            var lines = log.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder();
            var bad = lines.Where(l => !Regex.IsMatch(l, @"\] frame \d+: stereo draws") &&   // per-frame counters ("failed 0")
                                       Regex.IsMatch(l, @"\b(fail(s|ed|ure)?|error|crash(ed)?|exception|fault|giving up|not found|unavailable|could not|left without|stall(ed)?)\b", RegexOptions.IgnoreCase))
                           .Distinct().Take(60).ToList();
            sb.AppendLine("OGL3D.log, lines that look wrong (" + bad.Count + "):");
            foreach (var l in bad) sb.AppendLine("  " + l);
            sb.AppendLine("OGL3D.log, first lines:");
            foreach (var l in lines.Where(l => Regex.IsMatch(l, @"^\d\d:\d\d")).Take(12)) sb.AppendLine("  " + l);
            sb.AppendLine("OGL3D.log, last lines:");
            foreach (var l in lines.Where(l => Regex.IsMatch(l, @"^\d\d:\d\d")).Reverse().Take(25).Reverse()) sb.AppendLine("  " + l);
            return sb.ToString();
        }

        // the user name in paths is not needed to fix anything
        static string Redact(string text)
        {
            string user = Environment.UserName;
            return string.IsNullOrEmpty(user) ? text : Regex.Replace(text, @"(\\Users\\)" + Regex.Escape(user) + @"(?=\\|\b)", "$1<user>", RegexOptions.IgnoreCase);
        }

        static string Safe(string s)
        {
            s = Regex.Replace(s ?? "game", @"[^\w\- ]", "").Trim();
            return s.Length > 40 ? s.Substring(0, 40) : s;
        }

        static string WindowsVersion()
        {
            using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                if (k == null) return Environment.OSVersion.ToString();
                string name = Convert.ToString(k.GetValue("ProductName")), build = Convert.ToString(k.GetValue("CurrentBuild"));
                int b;
                if (int.TryParse(build, out b) && b >= 22000) name = name.Replace("Windows 10", "Windows 11");   // the registry name was never updated
                return name + " " + k.GetValue("DisplayVersion") + " (build " + build + "." + k.GetValue("UBR") + ")";
            }
        }
    }
}
