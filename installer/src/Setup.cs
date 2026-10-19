// Install and uninstall. Everything the installer adds, moves aside or changes is written to
// Flato3D_install.json next to the game exe, so uninstalling puts the folder back as it was.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Flato3D
{
    class Options
    {
        public string Output = "auto";   // auto (SR display) | sbs | tab
        public double Separation = 25;
        public bool Upscale;             // [Display] upscale: the game renders at a lower resolution, shown at the native one
    }

    static class Setup
    {
        public const string ManifestName = "Flato3D_install.json", BackupDir = "Flato3D_backup";

        // graphics DLLs a game folder may already hold from another tool (geo-11, dgVoodoo, ReShade, ...)
        static readonly string[] Conflicts = { "opengl32.dll", "version.dll", "d3d8.dll", "d3d9.dll", "ddraw.dll", "d3d10.dll", "d3d10_1.dll",
                                               "d3d10core.dll", "d3d11.dll", "dxgi.dll", "nvapi64.dll", "nvapi.dll" };

        // OGL3D for ordinary 3D games (no [Flat] section, not installed here): a Geo Gaming Hub install
        public static bool PlainStereo(string exeDir)
        {
            if (exeDir == null || File.Exists(Path.Combine(exeDir, ManifestName))) return false;
            string gl = Path.Combine(exeDir, "opengl32.dll"), ini = Path.Combine(exeDir, "OGL3D.ini");
            return File.Exists(gl) && Pe.HasAscii(gl, "OGL3D") &&
                   !(File.Exists(ini) && Regex.IsMatch(File.ReadAllText(ini), @"^\[Flat\]", RegexOptions.Multiline));
        }

        public static string Status(string exeDir)
        {
            if (exeDir == null) return "";
            var man = LoadManifest(exeDir);
            if (man != null)
            {
                if (LoaderBlocked(exeDir))
                    return L.T("설치됨 (version.dll 없음: 백신이 막았을 수 있음)", "installed (version.dll missing: an antivirus may have blocked it)");
                return L.T("설치됨", "installed");
            }
            string gl = Path.Combine(exeDir, "opengl32.dll");
            if (File.Exists(gl) && Pe.HasAscii(gl, "OGL3D"))
            {
                string ini = Path.Combine(exeDir, "OGL3D.ini");
                bool flat = File.Exists(ini) && Regex.IsMatch(File.ReadAllText(ini), @"^\[Flat\]", RegexOptions.Multiline);
                return flat ? L.T("수동 설치 (기록 없음)", "manual install (no record)") : L.T("OGL3D (3D 게임용)", "OGL3D (for 3D games)");
            }
            return "";
        }

        // The installed loader is gone or unreadable: an antivirus quarantined or blocked it.
        public static bool LoaderBlocked(string exeDir)
        {
            var man = exeDir == null ? null : LoadManifest(exeDir);
            bool loader = Json.Arr(man, "added").Any(a => string.Equals((string)a, "version.dll", StringComparison.OrdinalIgnoreCase));
            return loader && !Defender.Readable(Path.Combine(exeDir, "version.dll"));
        }

        // Flato3D set up by hand (before the installer): OGL3D with a [Flat] section but no install record.
        public static bool ManualInstall(string exeDir)
        {
            return exeDir != null && LoadManifest(exeDir) == null && Status(exeDir) == L.T("수동 설치 (기록 없음)", "manual install (no record)");
        }

        // Files the route puts next to the exe: (source, name).
        static List<KeyValuePair<string, string>> Files(string route, string arch, HashSet<string> imports)
        {
            var f = new List<KeyValuePair<string, string>>();
            Action<string, string> add = (src, name) =>
            {
                if (src == null || !File.Exists(src)) throw new Exception(L.T("설치 파일이 없습니다: ", "missing payload file: ") + (src ?? name));
                f.Add(new KeyValuePair<string, string>(src, name));
            };
            add(Payload.OpenGl32(arch), "opengl32.dll");
            if (route == Route.OpenGL) return f;
            add(Payload.Loader(arch), "version.dll");
            if (route == Route.D3D9)
            {
                string w = Payload.WineD3D(arch, false);
                add(w == null ? null : Path.Combine(w, "wined3d.dll"), "wined3d.dll");
                add(w == null ? null : Path.Combine(w, "d3d9.dll"), "d3d9.dll");
                if (imports.Contains("d3d8.dll")) add(Path.Combine(w, "d3d8.dll"), "d3d8.dll");
                if (imports.Contains("ddraw.dll")) add(Path.Combine(w, "ddraw.dll"), "ddraw.dll");
            }
            else
            {
                // renamed copies: the NVIDIA OpenGL driver presents through the system d3d11/dxgi and must not
                // pick up WineD3D's; the loader sends the game to w3d11.dll / wxgi.dll
                string w = Payload.WineD3D(arch, true);
                foreach (string n in new[] { "wined3d.dll", "w3d11.dll", "wxgi.dll", "w3d10core.dll" })
                    if (w != null && File.Exists(Path.Combine(w, n))) add(Path.Combine(w, n), n);
                    else if (n != "w3d10core.dll") add(null, n);
                // Direct3D 10 games load these by their own names (their imports point at the renamed copies)
                foreach (string n in new[] { "d3d10.dll", "d3d10_1.dll" })
                    if (imports.Contains(n)) add(w == null ? null : Path.Combine(w, n), n);
            }
            return f;
        }

        // A file this runtime put there (any build of it): replaced without a backup.
        static bool Ours(string file, string payloadSrc)
        {
            if (payloadSrc != null && File.Exists(payloadSrc) && new FileInfo(file).Length == new FileInfo(payloadSrc).Length &&
                File.ReadAllBytes(file).SequenceEqual(File.ReadAllBytes(payloadSrc))) return true;
            string n = Path.GetFileName(file).ToLowerInvariant();
            if (n == "opengl32.dll") return Pe.HasAscii(file, "OGL3D");
            if (n == "version.dll") return Pe.HasAscii(file, "Flato3D loader");
            if (n.StartsWith("w") || n == "wined3d.dll" || n == "d3d9.dll" || n == "d3d8.dll" || n == "ddraw.dll") return Pe.HasAscii(file, "wined3d");
            return false;
        }

        public static string Install(Game g, string exe, string route, Options o)
        {
            var log = new StringBuilder();
            if (route == Route.None) throw new Exception(L.T("이 게임은 이 방법으로 3D를 할 수 없습니다.", "This game cannot be done this way."));
            string dir = Path.GetDirectoryName(exe), arch = Pe.Arch(exe);
            if (arch == null) throw new Exception(L.T("exe의 32/64비트를 읽지 못했습니다.", "Could not read whether the exe is 32- or 64-bit."));
            var imports = Pe.Imports(exe);
            var man = LoadManifest(dir) ?? new Dictionary<string, object>();
            var added = new List<object>(Json.Arr(man, "added"));
            var backups = new List<object>(Json.Arr(man, "backups"));
            var regs = new List<object>(Json.Arr(man, "registry"));
            var files = Files(route, arch, imports);

            // other tools' graphics DLLs go aside first
            string bdir = Path.Combine(dir, BackupDir);
            foreach (string n in Conflicts)
            {
                string f = Path.Combine(dir, n);
                if (!File.Exists(f)) continue;
                var mine = files.FirstOrDefault(x => string.Equals(x.Value, n, StringComparison.OrdinalIgnoreCase));
                bool listed = added.Any(a => string.Equals((string)a, n, StringComparison.OrdinalIgnoreCase));
                if (listed || Ours(f, mine.Key)) continue;
                Directory.CreateDirectory(bdir);
                string b = Path.Combine(bdir, n);
                if (File.Exists(b)) File.Delete(f);   // the first backup is the original
                else File.Move(f, b);
                backups.Add(n);
                log.AppendLine(L.T("다른 도구의 파일을 옮김: ", "moved another tool's file aside: ") + n + " -> " + BackupDir + "\\");
            }
            foreach (var kv in files)
            {
                File.Copy(kv.Key, Path.Combine(dir, kv.Value), true);
                if (!added.Any(a => string.Equals((string)a, kv.Value, StringComparison.OrdinalIgnoreCase))) added.Add(kv.Value);
                log.AppendLine(L.T("복사: ", "copied: ") + kv.Value + " (" + arch + ")");
            }

            // OGL3D.ini: kept when it is already ours (values saved in game with Ctrl+F7); only the output follows the choice
            string ini = Path.Combine(dir, "OGL3D.ini");
            // (or one with a [Flat] section: a Flato3D setup made by hand before the installer)
            bool keep = File.Exists(ini) && (added.Any(a => string.Equals((string)a, "OGL3D.ini", StringComparison.OrdinalIgnoreCase)) ||
                                             Regex.IsMatch(File.ReadAllText(ini, Encoding.UTF8), @"^\[Flat\]", RegexOptions.Multiline));
            string text = keep ? File.ReadAllText(ini, Encoding.UTF8) : File.ReadAllText(Payload.IniTemplate(), Encoding.UTF8);
            if (keep && !added.Any(a => string.Equals((string)a, "OGL3D.ini", StringComparison.OrdinalIgnoreCase))) added.Add("OGL3D.ini");
            if (!keep)
            {
                text = IniSet(text, "Flat", "enable", "1");
                text = IniSet(text, "Flat", "rule", DefaultRule(g));
                text = IniSet(text, "Flat", "depth_screen", "0.3");
                if (route != Route.OpenGL) text = IniSet(text, "Display", "swap_interval", "1");
                if (Library.IsUnity(exe))
                {
                    // Unity draws each parallax plane separately (often from one atlas): a layer per draw,
                    // with more of the depth range for the far layers (Huntdown); its passes (2D lights) set
                    // the viewport between draws, so the AGS rule "later batches are UI" would keep the whole
                    // scene at the screen (Blade Assault)
                    text = IniSet(text, "Flat", "group", "draw");
                    text = IniSet(text, "Flat", "order_curve", "0.5");
                    text = IniSet(text, "Flat", "screen_batch", "0");
                }
                foreach (var sec in Json.Obj(g.Profile, "ini") ?? new Dictionary<string, object>())
                    foreach (var kv in (Dictionary<string, object>)sec.Value)
                        text = IniSet(text, sec.Key, kv.Key, Convert.ToString(kv.Value, CultureInfo.InvariantCulture));
                text = IniSet(text, "Stereo", "separation", o.Separation.ToString("0.##", CultureInfo.InvariantCulture));
                if (File.Exists(ini) && !backups.Contains("OGL3D.ini"))
                {
                    Directory.CreateDirectory(bdir);
                    File.Move(ini, Path.Combine(bdir, "OGL3D.ini"));
                    backups.Add("OGL3D.ini");
                }
                added.Add("OGL3D.ini");
            }
            text = IniSet(text, "Stereo", "output", o.Output);
            text = IniSet(text, "Display", "upscale", o.Upscale ? "1" : "0");
            File.WriteAllText(ini, text, new UTF8Encoding(true));
            log.AppendLine((keep ? L.T("OGL3D.ini 유지 (출력, 업스케일만 변경): ", "kept OGL3D.ini (output, upscale changed): ") : L.T("OGL3D.ini 작성: ", "wrote OGL3D.ini: ")) +
                           "output = " + o.Output + ", upscale = " + (o.Upscale ? "1" : "0"));

            foreach (var r in Json.Arr(g.Profile, "registry"))
            {
                var e = (Dictionary<string, object>)r;
                string key = Json.Str(e, "key"), name = Json.Str(e, "name");
                if (key == null || !key.StartsWith(@"HKCU\", StringComparison.OrdinalIgnoreCase) || regs.Any(x => Json.Str((Dictionary<string, object>)x, "key") == key && Json.Str((Dictionary<string, object>)x, "name") == name)) continue;
                using (var k = Registry.CurrentUser.CreateSubKey(key.Substring(5)))
                {
                    object old = k.GetValue(name);
                    regs.Add(new Dictionary<string, object> { { "key", key }, { "name", name }, { "old", old == null ? null : Convert.ToString(old, CultureInfo.InvariantCulture) } });
                    k.SetValue(name, Convert.ToInt32(Json.Str(e, "value"), CultureInfo.InvariantCulture), RegistryValueKind.DWord);
                }
                log.AppendLine(L.T("레지스트리: ", "registry: ") + key + " " + name + " = " + Json.Str(e, "value"));
            }

            // laptops with an NVIDIA GPU next to another one: the game runs on the NVIDIA GPU (Windows graphics
            // setting "high performance"), the only GPU Flato3D is supported on
            const string gpuKey = @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences";
            if (Gpu.Hybrid && !regs.Any(x => Json.Str((Dictionary<string, object>)x, "key") == gpuKey && Json.Str((Dictionary<string, object>)x, "name") == exe))
                using (var k = Registry.CurrentUser.CreateSubKey(gpuKey.Substring(5)))
                {
                    object old = k.GetValue(exe);
                    regs.Add(new Dictionary<string, object> { { "key", gpuKey }, { "name", exe }, { "type", "sz" }, { "old", old as string } });
                    k.SetValue(exe, "GpuPreference=2;", RegistryValueKind.String);
                    log.AppendLine(L.T("그래픽 설정: 이 게임은 NVIDIA GPU(고성능)로 실행", "graphics setting: this game runs on the NVIDIA GPU (high performance)"));
                }
            if (Gpu.Known && !Gpu.Nvidia) log.AppendLine(Gpu.Warning());
            if (route != Route.OpenGL)
            {
                log.AppendLine(Defender.Notice());
                if (!Defender.Readable(Path.Combine(dir, "version.dll")))
                    log.AppendLine(L.T("경고: 백신이 version.dll을 막았습니다. [Defender 예외]를 누른 뒤 다시 설치하세요.",
                                       "Warning: an antivirus blocked version.dll. Press [Defender exclusion], then install again."));
            }

            man["version"] = 1;
            man["date"] = DateTime.Now.ToString("s");
            man["game"] = g.Title;
            man["exe"] = Path.GetFileName(exe);
            man["route"] = route;
            man["arch"] = arch;
            man["added"] = added;
            man["backups"] = backups;
            man["registry"] = regs;
            File.WriteAllText(Path.Combine(dir, ManifestName), Json.Dump(man), new UTF8Encoding(false));
            log.AppendLine(L.T("설치 완료: ", "installed: ") + Route.Label(route) + ", " + arch);
            return log.ToString();
        }

        public static string Uninstall(string exeDir)
        {
            var log = new StringBuilder();
            var man = LoadManifest(exeDir);
            if (man == null) throw new Exception(L.T("설치 기록(Flato3D_install.json)이 없습니다.", "No install record (Flato3D_install.json)."));
            foreach (var a in Json.Arr(man, "added"))
            {
                string f = Path.Combine(exeDir, (string)a);
                if (File.Exists(f)) { File.Delete(f); log.AppendLine(L.T("삭제: ", "deleted: ") + a); }
            }
            foreach (string pat in new[] { "OGL3D.log", "OGL3D_frame*.bmp", "OGL3D_step*.bmp", "OGL3D_flicker*.bmp", "Flato3D_loader_*.log" })
                foreach (string f in Directory.GetFiles(exeDir, pat)) File.Delete(f);
            string bdir = Path.Combine(exeDir, BackupDir);
            foreach (var b in Json.Arr(man, "backups"))
            {
                string src = Path.Combine(bdir, (string)b), dst = Path.Combine(exeDir, (string)b);
                if (File.Exists(src) && !File.Exists(dst)) { File.Move(src, dst); log.AppendLine(L.T("되돌림: ", "restored: ") + b); }
            }
            if (Directory.Exists(bdir) && !Directory.EnumerateFileSystemEntries(bdir).Any()) Directory.Delete(bdir);
            foreach (var r in Json.Arr(man, "registry"))
            {
                var e = (Dictionary<string, object>)r;
                string key = Json.Str(e, "key"), name = Json.Str(e, "name"), old = Json.Str(e, "old");
                using (var k = Registry.CurrentUser.OpenSubKey(key.Substring(5), true))
                {
                    if (k == null) continue;
                    if (old == null) k.DeleteValue(name, false);
                    else if (Json.Str(e, "type") == "sz") k.SetValue(name, old, RegistryValueKind.String);
                    else k.SetValue(name, Convert.ToInt32(old, CultureInfo.InvariantCulture), RegistryValueKind.DWord);
                }
                log.AppendLine(L.T("레지스트리 되돌림: ", "registry restored: ") + key + " " + name);
            }
            File.Delete(Path.Combine(exeDir, ManifestName));
            log.AppendLine(L.T("제거 완료", "uninstalled"));
            return log.ToString();
        }

        // The user's latest status report for a game installed here (result, date, note), or null.
        public static Dictionary<string, object> UserStatus(string exeDir)
        {
            return exeDir == null ? null : Json.Obj(LoadManifest(exeDir), "user_status");
        }

        // The user's latest status report for the game, shown in the game list (only for games installed here).
        public static void RecordUserStatus(string dir, string result, string note)
        {
            var man = LoadManifest(dir);
            if (man == null) return;
            man["user_status"] = new Dictionary<string, object> {
                { "result", result }, { "date", DateTime.Now.ToString("MM-dd", CultureInfo.InvariantCulture) }, { "note", note ?? "" } };
            File.WriteAllText(Path.Combine(dir, ManifestName), Json.Dump(man), new UTF8Encoding(false));
        }

        // Defender exclusions added for the game (removed again on request when uninstalling)
        public static List<string> DefenderExclusions(string dir)
        {
            return Json.Arr(LoadManifest(dir), "defender").Select(x => (string)x).ToList();
        }

        public static void RecordDefender(string dir, string path, bool add)
        {
            var man = LoadManifest(dir);
            if (man == null) return;
            var list = DefenderExclusions(dir);
            list.RemoveAll(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));
            if (add) list.Add(path);
            man["defender"] = list.Cast<object>().ToList();
            File.WriteAllText(Path.Combine(dir, ManifestName), Json.Dump(man), new UTF8Encoding(false));
        }

        static Dictionary<string, object> LoadManifest(string dir)
        {
            string f = Path.Combine(dir, ManifestName);
            return File.Exists(f) ? Json.Load(f) as Dictionary<string, object> : null;
        }

        // feet for adventure games (sprites stand where their bottom edge is), drawing order otherwise
        static string DefaultRule(Game g)
        {
            return Json.Str(g.Candidate, "depth_rule") == "feet_y" ? "feet" : "order";
        }

        // Value of key in [section], or null.
        public static string IniGet(string text, string section, string key)
        {
            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            int start = lines.FindIndex(l => l.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
            if (start < 0) return null;
            var rx = new Regex(@"^\s*" + Regex.Escape(key) + @"\s*=\s*(.*?)\s*$", RegexOptions.IgnoreCase);
            for (int i = start + 1; i < lines.Count && !lines[i].TrimStart().StartsWith("["); ++i)
            {
                var m = rx.Match(lines[i]);
                if (m.Success) return m.Groups[1].Value;
            }
            return null;
        }

        // Sets key = value in [section]; adds the key (or the section) when missing.
        public static string IniSet(string text, string section, string key, string value)
        {
            string nl = text.Contains("\r\n") ? "\r\n" : "\n";
            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
            int start = lines.FindIndex(l => l.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
            if (start < 0)
            {
                lines.Add("");
                lines.Add("[" + section + "]");
                lines.Add(key + " = " + value);
                return string.Join(nl, lines);
            }
            int end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith("["));
            if (end < 0) end = lines.Count;
            var rx = new Regex(@"^\s*" + Regex.Escape(key) + @"\s*=", RegexOptions.IgnoreCase);
            for (int i = start + 1; i < end; ++i)
                if (rx.IsMatch(lines[i])) { lines[i] = key + " = " + value; return string.Join(nl, lines); }
            lines.Insert(start + 1, key + " = " + value);
            return string.Join(nl, lines);
        }
    }
}
