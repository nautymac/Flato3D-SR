// What the PC itself means for Flato3D: the GPU (the SR edition is for NVIDIA, the Intel Arc edition for Intel),
// Windows Defender, which may wrongly flag the loader (version.dll) as malware, and the 3D One's AstraDepth3D app,
// which weaves the Intel Arc edition's side-by-side picture.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

namespace Flato3D
{
    static class Gpu
    {
        static List<string> names;

        public static List<string> Names()
        {
            if (names != null) return names;
            names = new List<string>();
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                    foreach (ManagementObject o in s.Get())
                    {
                        string n = Convert.ToString(o["Name"]);
                        if (!string.IsNullOrEmpty(n) && !names.Contains(n)) names.Add(n);
                    }
            }
            catch { }
            return names;
        }

        static bool IsNvidia(string n) { return n.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0; }
        static bool IsIntel(string n) { return n.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0; }
        static bool IsVirtual(string n) { return Regex.IsMatch(n, "Basic Display|Basic Render|Remote|Virtual|Meta|Parsec|spacedesk|Indirect|Microsoft", RegexOptions.IgnoreCase); }

        public static bool Nvidia { get { return Names().Any(IsNvidia); } }
        public static bool Intel { get { return Names().Any(IsIntel); } }
        // an NVIDIA GPU next to another real one (laptops): Windows may start a game on the other one
        public static bool Hybrid { get { return Nvidia && Names().Any(n => !IsNvidia(n) && !IsVirtual(n)); } }
        public static bool Known { get { return Names().Count > 0; } }

        // The edition's GPU is not here: the SR package wants NVIDIA, the Intel Arc package an Intel GPU.
        public static bool Mismatch { get { return Known && (AppInfo.IntelArc ? !Intel : !Nvidia); } }

        public static string Warning()
        {
            if (AppInfo.IntelArc)
                return L.T("Intel GPU가 없습니다: Flato3D-IntelARC는 Intel Arc GPU(3D One)용입니다. NVIDIA GPU와 SR 디스플레이에는 Flato3D-SR을 쓰세요.",
                           "No Intel GPU: Flato3D-IntelARC is for Intel Arc GPUs (3D One). For NVIDIA GPUs and SR displays use Flato3D-SR.");
            return L.T("NVIDIA GPU가 없습니다: Flato3D는 NVIDIA GPU에서만 동작을 보장합니다. Intel Arc GPU(3D One)에는 Flato3D-IntelARC를 쓰세요.",
                       "No NVIDIA GPU: Flato3D is only supported on NVIDIA GPUs. For an Intel Arc GPU (3D One) use Flato3D-IntelARC.");
        }
    }

    // The 3D One laptop's AstraDepth3D app weaves a full-screen window for its lenticular panel. Its gamelist.json
    // (next to the app, read once when it starts) lists the exes whose picture is already side by side (type 1);
    // every other window gets its AI 2D-to-3D conversion. Flato3D-IntelARC puts the game there on install, so the
    // app weaves the two eyes Flato3D renders instead of guessing depth from one picture.
    static class Astra
    {
        public static string Dir
        {
            get
            {
                foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) })
                {
                    string d = Path.Combine(root, "AstraDepth3D");
                    if (File.Exists(Path.Combine(d, "AstraDepth3D.exe"))) return d;
                }
                return null;
            }
        }
        public static bool Installed { get { return Dir != null && File.Exists(Path.Combine(Dir, "gamelist.json")); } }
        public static bool Running { get { return Process.GetProcessesByName("AstraDepth3D").Length > 0; } }

        // The list matches a running game by its exe name and the last two parts of its path ("<folder>\<exe>").
        public static string EntryPath(string exe) { return Path.GetFileName(Path.GetDirectoryName(exe)) + "\\" + Path.GetFileName(exe); }

        static string Json(string s) { return s.Replace("\\", "\\\\").Replace("\"", "\\\""); }

        // The file's bytes as they are (its name field is Chinese in GBK; only ASCII is added), so nothing else changes.
        static readonly Encoding Raw = Encoding.GetEncoding(28591);

        public static bool Listed(string exe)
        {
            if (!Installed) return false;
            string text = File.ReadAllText(Path.Combine(Dir, "gamelist.json"), Raw);
            return Regex.IsMatch(text, EntryPattern(exe));
        }

        static string EntryPattern(string exe)
        {
            return @"\{[^{}]*""title""\s*:\s*""" + Regex.Escape(Json(Path.GetFileName(exe))) + @"""[^{}]*""path""\s*:\s*""" + Regex.Escape(Json(EntryPath(exe))) + @"""[^{}]*\}";
        }

        // Adds the game as a side-by-side source (type 1). False when the file could not be written (the
        // permission prompt was declined, or the list has no "games" array).
        public static bool Register(string exe, out string why)
        {
            why = "";
            string file = Path.Combine(Dir, "gamelist.json");
            string text = File.ReadAllText(file, Raw);
            if (Regex.IsMatch(text, EntryPattern(exe))) return true;
            var m = Regex.Match(text, @"""games""\s*:\s*\[");
            if (!m.Success) { why = "no games array"; return false; }
            int open = m.Index + m.Length, depth = 1, close = -1;
            for (int i = open; i < text.Length && close < 0; ++i)
            {
                if (text[i] == '[') ++depth;
                else if (text[i] == ']' && --depth == 0) close = i;
            }
            if (close < 0) { why = "games array not closed"; return false; }
            bool empty = text.Substring(open, close - open).Trim().Length == 0;
            string entry = (empty ? "" : ",") + "\n      {\n        \"title\": \"" + Json(Path.GetFileName(exe)) + "\",\n        \"path\": \"" + Json(EntryPath(exe)) + "\",\n        \"type\": \"1\"\n      }\n    ";
            // after the last entry's closing brace (any whitespace before ']' is replaced)
            int end = close;
            while (end > open && char.IsWhiteSpace(text[end - 1])) --end;
            return Write(file, text.Substring(0, end) + entry + text.Substring(close), out why);
        }

        public static bool Unregister(string exe, out string why)
        {
            why = "";
            string file = Path.Combine(Dir, "gamelist.json");
            string text = File.ReadAllText(file, Raw);
            var m = Regex.Match(text, EntryPattern(exe));
            if (!m.Success) return true;
            int start = m.Index, end = m.Index + m.Length;
            // the comma that joined it: before (last entry) or after (not the last)
            int b = start;
            while (b > 0 && char.IsWhiteSpace(text[b - 1])) --b;
            int a = end;
            while (a < text.Length && char.IsWhiteSpace(text[a])) ++a;
            if (a < text.Length && text[a] == ',') end = a + 1;
            else if (b > 0 && text[b - 1] == ',') start = b - 1;
            return Write(file, text.Substring(0, start) + text.Substring(end), out why);
        }

        // Program Files: an elevated copy (Windows asks for permission); the first write keeps a backup beside the file.
        static bool Write(string file, string text, out string why)
        {
            why = "";
            string tmp = Path.Combine(Path.GetTempPath(), "Flato3D_gamelist_" + DateTime.Now.ToString("HHmmss") + ".json");
            File.WriteAllText(tmp, text, Raw);
            string bak = file + ".flato3d-backup";
            string ps = "if (-not (Test-Path '" + bak.Replace("'", "''") + "')) { Copy-Item '" + file.Replace("'", "''") + "' '" + bak.Replace("'", "''") + "' }; " +
                        "Copy-Item '" + tmp.Replace("'", "''") + "' '" + file.Replace("'", "''") + "' -Force";
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " +
                                           Convert.ToBase64String(Encoding.Unicode.GetBytes(ps)))
            { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
            try
            {
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(60000);
                    if (!(p.HasExited && p.ExitCode == 0)) { why = "copy failed"; return false; }
                }
            }
            catch (System.ComponentModel.Win32Exception) { why = L.T("관리자 권한을 거부함", "permission declined"); return false; }
            finally { try { File.Delete(tmp); } catch { } }
            string now = File.ReadAllText(file, Raw);
            if (now != text) { why = "file unchanged"; return false; }
            return true;
        }

        // The app reads the list only when it starts: close and start it again (it runs as the user).
        public static bool Restart()
        {
            string exe = Dir == null ? null : Path.Combine(Dir, "AstraDepth3D.exe");
            if (exe == null) return false;
            foreach (var p in Process.GetProcessesByName("AstraDepth3D"))
            {
                try { p.Kill(); p.WaitForExit(10000); } catch { }
            }
            try { Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Dir, UseShellExecute = true }); return true; }
            catch { return false; }
        }
    }

    static class Defender
    {
        public static string Notice()
        {
            return L.T("참고: Windows Defender가 version.dll(Flato3D 로더)을 바이러스(예: Trojan:Win32/Cloxer)로 잘못 진단할 수 있습니다. " +
                       "게임이 불러오는 DLL을 WineD3D로 돌리는 방식 때문이며 악성 코드가 아닙니다(소스: github.com/nautymac/Flato3D-SR). " +
                       "차단되면 3D가 나오지 않습니다. [Defender 예외]를 누르면 이 게임의 version.dll만 예외로 등록합니다.",
                       "Note: Windows Defender may wrongly flag version.dll (the Flato3D loader) as malware (e.g. Trojan:Win32/Cloxer). " +
                       "It sends the DLLs the game loads to WineD3D; it is not malware (source: github.com/nautymac/Flato3D-SR). " +
                       "When it is blocked there is no 3D. [Defender exclusion] adds this game's version.dll only.");
        }

        // The file can be read: an antivirus has not blocked or removed it.
        public static bool Readable(string path)
        {
            try { using (var f = File.OpenRead(path)) { f.ReadByte(); return true; } }
            catch { return false; }
        }

        // Add-/Remove-MpPreference for one path in an elevated PowerShell (Windows asks for permission).
        // False when the user declined, or when Defender is not the active antivirus.
        public static bool Exclude(string path, bool add)
        {
            string ps = (add ? "Add" : "Remove") + "-MpPreference -ExclusionPath '" + path.Replace("'", "''") + "'";
            var psi = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand " +
                                           Convert.ToBase64String(Encoding.Unicode.GetBytes(ps)))
            { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
            try
            {
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(60000);
                    return p.HasExited && p.ExitCode == 0;
                }
            }
            catch (System.ComponentModel.Win32Exception) { return false; }   // the permission prompt was declined
        }
    }
}
