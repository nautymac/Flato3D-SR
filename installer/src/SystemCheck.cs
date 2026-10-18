// What the PC itself means for Flato3D: the GPU (only NVIDIA is supported; Intel's OpenGL driver broke
// shaders) and Windows Defender, which may wrongly flag the loader (version.dll) as malware.
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
        static bool IsVirtual(string n) { return Regex.IsMatch(n, "Basic Display|Basic Render|Remote|Virtual|Meta|Parsec|spacedesk|Indirect|Microsoft", RegexOptions.IgnoreCase); }

        public static bool Nvidia { get { return Names().Any(IsNvidia); } }
        // an NVIDIA GPU next to another real one (laptops): Windows may start a game on the other one
        public static bool Hybrid { get { return Nvidia && Names().Any(n => !IsNvidia(n) && !IsVirtual(n)); } }
        public static bool Known { get { return Names().Count > 0; } }

        public static string Warning()
        {
            return L.T("NVIDIA GPU가 없습니다: Flato3D는 NVIDIA GPU에서만 동작을 보장합니다. Intel GPU에서는 셰이더가 깨진 적이 있습니다.",
                       "No NVIDIA GPU: Flato3D is only supported on NVIDIA GPUs. On Intel GPUs shaders have broken.");
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
