#!/usr/bin/env python3
"""Flato3D for Linux: stereo depth for flat 2D Steam/Proton games.

Runs in the DroidDeck or Steam Deck desktop and uses the Flato3D-SR package next to it
(ogl3d/, wined3d/, data/). Same choices as the Windows installer (profile, route, depth
rule, separation), with Proton in place of the Windows loader:
  OpenGL games  -> opengl32.dll (OGL3D) next to the exe
  Direct3D 8-11 -> Proton's own WineD3D (PROTON_USE_WINED3D=1), plus OGL3D
Output is half side-by-side, which DroidDeck shows with Cardboard off
(or Cardboard "Side-by-side 3D" + Viewer "No lens").
"""
import argparse
import glob
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import time

VERSION = "0.1.0"
STATE_DIR = ".flato3d_linux"
MANIFEST = "manifest.json"
OUTPUTS = ["sbs", "tab"]
DEFAULT_SEPARATION = 25.0
# graphics DLLs another tool may have left next to the exe (geo-11, dgVoodoo, ReShade, ...)
CONFLICTS = ["opengl32.dll", "version.dll", "d3d8.dll", "d3d9.dll", "ddraw.dll", "d3d10.dll", "d3d10_1.dll",
             "d3d10core.dll", "d3d11.dll", "dxgi.dll", "nvapi64.dll", "nvapi.dll", "wined3d.dll"]
RUNTIME_GLOBS = ["OGL3D.log", "OGL3D_*.bmp"]
NOT_GAMES = re.compile(r"Proton|Steamworks|Steam Linux Runtime|SteamVR|Redistributable", re.I)
NOT_EXE = re.compile(r"unins|crash|setup|config|launcher|redist|vcredist|dxsetup|dotnet|update|helper|report|"
                     r"unitycrashhandler|ue4prereq|easyanticheat|cefsharp|nw\.exe", re.I)

KO = os.environ.get("LANG", "").lower().startswith("ko") or os.environ.get("FLATO3D_LANG") == "ko"


def T(ko, en):
    return ko if KO else en


def norm(s):
    return re.sub(r"[^a-z0-9]", "", (s or "").lower())


# ---------- package ----------

def package_dir(arg):
    here = os.path.dirname(os.path.realpath(__file__))
    for c in [arg, os.environ.get("FLATO3D_PACKAGE"), here, os.path.join(here, ".."),
              os.path.expanduser("~/Flato3D-SR")]:
        if c and os.path.isfile(os.path.join(c, "ogl3d", "OGL3D.ini")):
            return os.path.realpath(c)
    return None


def load_data(pkg):
    def load(name):
        try:
            with open(os.path.join(pkg, "data", name), encoding="utf-8-sig") as f:
                d = json.load(f)
            return d.get("games", []) if isinstance(d, dict) else d
        except (OSError, ValueError):
            return []
    return load("game_profiles.json"), load("game_candidates.json")


def match(game, profiles, candidates):
    folder = os.path.basename(game["dir"].rstrip("/"))
    appid = game.get("appid") or ""
    prof = next((p for p in profiles if (appid and str(p.get("steam_appid", "")) == appid) or
                 (p.get("folder") or "").lower() == folder.lower()), None)
    nt, nf = norm(game["name"]), norm(folder)
    cand = next((c for c in candidates if (appid and str(c.get("steam_appid", "")) == appid) or
                 (norm(c.get("title")) and norm(c.get("title")) in (nt, nf))), None)
    return prof, cand


# ---------- Steam library discovery ----------

def steam_roots(extra):
    home = os.path.expanduser("~")
    cands = list(extra) + [os.path.join(home, ".local/share/Steam"), os.path.join(home, ".steam/steam"),
                           os.path.join(home, ".steam/root"), "/mnt/droiddeck-sd/x86-64", "/mnt/droiddeck-sd"]
    libs, seen = [], set()

    def add(p):
        if not p or not os.path.isdir(os.path.join(p, "steamapps")):
            return
        rp = os.path.realpath(p)
        if rp not in seen:
            seen.add(rp)
            libs.append(p)

    for root in cands:
        add(root)
        vdf = os.path.join(root, "steamapps", "libraryfolders.vdf")
        try:
            with open(vdf, encoding="utf-8", errors="replace") as f:
                for m in re.finditer(r'"path"\s+"([^"]+)"', f.read()):
                    add(m.group(1).replace("\\\\", "/"))
        except OSError:
            pass
    return libs


def steam_games(libs):
    games = []
    for lib in libs:
        sa = os.path.join(lib, "steamapps")
        for entry in sorted(os.listdir(sa)):
            if not (entry.startswith("appmanifest_") and entry.endswith(".acf")):
                continue
            try:
                with open(os.path.join(sa, entry), encoding="utf-8", errors="replace") as f:
                    kv = dict(re.findall(r'"(appid|name|installdir)"\s+"([^"]*)"', f.read()))
            except OSError:
                continue
            if not kv.get("installdir") or kv.get("appid") == "228980" or NOT_GAMES.search(kv.get("name", "")):
                continue
            d = os.path.join(sa, "common", kv["installdir"])
            if os.path.isdir(d):
                games.append({"appid": kv.get("appid", ""), "name": kv.get("name", kv["installdir"]), "dir": d})
    return sorted(games, key=lambda g: g["name"].lower())


def find_exes(game, prof):
    """Candidate game exes, best first (the profile's, else the Windows installer's FindExe rules)."""
    d = game["dir"]
    if prof and prof.get("exe"):
        p = os.path.join(d, prof["exe"].replace("\\", "/"))
        if os.path.isfile(p):
            return [p]
    shipping = []
    for root, dirs, files in os.walk(d):
        if re.search(r"[/\\]Binaries[/\\]Win(64|32)$", root):
            shipping += [os.path.join(root, f) for f in files if re.search(r"-Shipping\.exe$", f, re.I)]
    if shipping:
        return sorted(shipping, key=lambda p: ("Win64" not in p, p))
    out = []
    for sub in ["", "bin", "Bin", "x64", "bin64", "Binaries", "game", "Game"]:
        s = os.path.join(d, sub)
        if not os.path.isdir(s):
            continue
        for f in sorted(os.listdir(s)):
            p = os.path.join(s, f)
            if f.lower().endswith(".exe") and os.path.isfile(p) and not NOT_EXE.search(f):
                out.append(p)
    nt, nf = norm(game["name"]), norm(os.path.basename(d))

    def score(p):
        n = norm(os.path.splitext(os.path.basename(p))[0])
        exact = n in (nt, nf)
        near = (n in nt or n in nf) and len(n) > 2
        return (-(2 if exact else 1 if near else 0), -os.path.getsize(p))
    return sorted(dict.fromkeys(out), key=score)


# ---------- PE inspection ----------

def pe_info(path):
    """Returns ("x64"|"x86"|None, set of imported dll names, lower case)."""
    try:
        with open(path, "rb") as f:
            data = f.read()
    except OSError:
        return None, set()
    try:
        if data[:2] != b"MZ":
            return None, set()
        pe = struct.unpack_from("<I", data, 0x3C)[0]
        if data[pe:pe + 4] != b"PE\0\0":
            return None, set()
        machine, nsec = struct.unpack_from("<HH", data, pe + 4)
        opt_size = struct.unpack_from("<H", data, pe + 20)[0]
        opt = pe + 24
        magic = struct.unpack_from("<H", data, opt)[0]
        arch = "x64" if machine == 0x8664 else "x86" if machine == 0x14C else None
        dd = opt + (112 if magic == 0x20B else 96)
        imp_rva = struct.unpack_from("<I", data, dd + 8)[0]
        secs = []
        for i in range(nsec):
            s = opt + opt_size + i * 40
            vsize, va, rsize, raw = struct.unpack_from("<IIII", data, s + 8)
            secs.append((va, max(vsize, rsize), raw))

        def off(rva):
            for va, size, raw in secs:
                if va <= rva < va + size:
                    return rva - va + raw
            return None
        names = set()
        o = off(imp_rva) if imp_rva else None
        while o is not None and o + 20 <= len(data):
            name_rva = struct.unpack_from("<I", data, o + 12)[0]
            if name_rva == 0:
                break
            no = off(name_rva)
            if no is not None:
                names.add(data[no:data.index(b"\0", no)].decode("latin-1").lower())
            o += 20
        return arch, names
    except (struct.error, ValueError):
        return None, set()


def is_unity(exe):
    d = os.path.dirname(exe)
    data = os.path.join(d, os.path.splitext(os.path.basename(exe))[0] + "_Data")
    return os.path.isfile(os.path.join(d, "UnityPlayer.dll")) or (os.path.isdir(data) and any(
        os.path.exists(os.path.join(data, n)) for n in ("Managed", "globalgamemanagers", "mainData", "data.unity3d")))


def unity_route(exe, cand):
    """The API Unity's last run reported (output_log.txt / Player.log), else the candidate list, else D3D11."""
    data = os.path.join(os.path.dirname(exe), os.path.splitext(os.path.basename(exe))[0] + "_Data")
    logs = [os.path.join(data, "output_log.txt")]
    try:
        with open(os.path.join(data, "app.info"), encoding="utf-8", errors="replace") as f:
            info = [l.strip() for l in f.read().splitlines()]
        if len(info) >= 2:
            # Proton prefix: steamapps/compatdata/<appid>/pfx/drive_c/users/steamuser/AppData/LocalLow
            for low in glob.glob(os.path.join(exe.split("/steamapps/")[0], "steamapps/compatdata/*/pfx/drive_c/users/steamuser/AppData/LocalLow")):
                logs.append(os.path.join(low, info[0], info[1], "Player.log"))
    except OSError:
        pass
    for log in sorted((l for l in logs if os.path.isfile(l)), key=os.path.getmtime, reverse=True):
        try:
            with open(log, encoding="utf-8", errors="replace") as f:
                head = f.read(16384)
        except OSError:
            continue
        m = re.search(r"Version:\s+(Direct3D (\d+)|OpenGL)[^\r\n]*", head)
        if m:
            why = T("Unity, 지난 실행: ", "Unity, last run: ") + m.group(0)[8:].strip()
            if m.group(2):
                return ("d3d11" if int(m.group(2)) >= 10 else "d3d9"), why
            return "opengl", why
    if (cand or {}).get("api") == "d3d9":
        return "d3d9", T("Unity, 후보 목록 (D3D9)", "Unity, candidate list (D3D9)")
    return "d3d11", T("Unity (기본 D3D11)", "Unity (D3D11 by default)")


def detect_route(exe, prof, cand):
    """Same order as the Windows installer: profile, exe imports, candidate list."""
    pr, papi = (prof or {}).get("route"), (prof or {}).get("api") or ""
    if pr == "opengl":
        return "opengl", T("프로필", "profile")
    if pr == "wined3d":
        return ("d3d9" if "9" in papi or " 8" in papi else "d3d11"), T("프로필", "profile")
    if str((cand or {}).get("kind")) == "1":
        return None, T("일반 입체 게임: GeoGamingHub(geo-11)로 설치", "ordinary stereo game: use GeoGamingHub (geo-11)")
    if exe and os.path.isfile(exe):
        _, imp = pe_info(exe)
        if "d3d12.dll" in imp or "vulkan-1.dll" in imp:
            return None, T("exe가 D3D12/Vulkan을 가져옴", "the exe imports D3D12/Vulkan")
        if is_unity(exe):
            return unity_route(exe, cand)
        if imp & {"d3d11.dll", "dxgi.dll", "d3d10.dll", "d3d10_1.dll"}:
            return "d3d11", T("exe가 D3D10/11을 가져옴", "the exe imports D3D10/11")
        if imp & {"d3d9.dll", "d3d8.dll", "ddraw.dll"}:
            return "d3d9", T("exe가 D3D8/9를 가져옴", "the exe imports D3D8/9")
        if "opengl32.dll" in imp:
            return "opengl", T("exe가 OpenGL을 가져옴", "the exe imports OpenGL")
        if exe.lower().endswith("-shipping.exe"):
            return "d3d11", T("Unreal Engine (기본 D3D11)", "Unreal Engine (D3D11 by default)")
    api = (cand or {}).get("api")
    if api in ("opengl", "opengl_alt"):
        return "opengl", T("후보 목록", "candidate list") + (T(" (게임을 OpenGL로 바꿔야 함)", " (switch the game to OpenGL)") if api == "opengl_alt" else "")
    if api in ("d3d9", "d3d11"):
        return api, T("후보 목록", "candidate list")
    if api in ("oneimage", "browser", "unsupported"):
        return None, {"oneimage": T("완성 화면 한 장만 나옴", "one finished image only"),
                      "browser": T("브라우저 엔진", "browser engine"), "unsupported": "D3D12/Vulkan"}[api]
    return "d3d11", T("판별 못함: D3D11로 시도", "unknown: trying D3D11")


def recommend(exe, prof, cand):
    ini = (prof or {}).get("ini") or {}
    stereo, flat = ini.get("Stereo") or {}, ini.get("Flat")
    r = {"separation": float(stereo.get("separation", DEFAULT_SEPARATION)), "flat": True}
    if (flat or {}).get("enable") in (0, "0") or (flat is None and str((cand or {}).get("kind")) == "1"):
        r["flat"] = False
    rule = "feet" if (cand or {}).get("depth_rule") == "feet_y" else "order"
    r["rule"] = (flat or {}).get("rule") or rule
    o = stereo.get("output")
    r["output"] = o if o in OUTPUTS else "sbs"
    return r


# ---------- ini text (keeps CRLF, same rules as the Windows installer's IniSet) ----------

def ini_set(text, section, key, value):
    nl = "\r\n" if "\r\n" in text else "\n"
    lines = text.replace("\r\n", "\n").split("\n")
    start = next((i for i, l in enumerate(lines) if l.strip().lower() == "[%s]" % section.lower()), None)
    if start is None:
        lines += ["", "[%s]" % section, "%s = %s" % (key, value)]
        return nl.join(lines)
    end = next((i for i in range(start + 1, len(lines)) if lines[i].lstrip().startswith("[")), len(lines))
    rx = re.compile(r"^\s*%s\s*=" % re.escape(key), re.I)
    for i in range(start + 1, end):
        if rx.match(lines[i]):
            lines[i] = "%s = %s" % (key, value)
            return nl.join(lines)
    lines.insert(start + 1, "%s = %s" % (key, value))
    return nl.join(lines)


def ini_get(text, section, key):
    lines = text.replace("\r\n", "\n").split("\n")
    start = next((i for i, l in enumerate(lines) if l.strip().lower() == "[%s]" % section.lower()), None)
    if start is None:
        return None
    rx = re.compile(r"^\s*%s\s*=\s*(.*?)\s*$" % re.escape(key), re.I)
    for l in lines[start + 1:]:
        if l.lstrip().startswith("["):
            break
        m = rx.match(l)
        if m:
            return m.group(1)
    return None


def cfg_set(text, section, key, value):
    """ini_set for a game's own file: an existing line keeps its spacing (acsetup.cfg's key=value)."""
    m = re.search(r"(?ims)^\[" + re.escape(section) + r"\][^\[]*?^(\s*" + re.escape(key) + r"\s*=[ \t]*)[^\r\n]*", text)
    if m:
        return text[:m.start(1) + len(m.group(1))] + value + text[m.end():]
    return ini_set(text, section, key, value)


def fmt(v):
    if isinstance(v, bool):
        return "1" if v else "0"
    if isinstance(v, float):
        return ("%.4f" % v).rstrip("0").rstrip(".")
    return str(v)


def read_text(path):
    with open(path, "rb") as f:
        return f.read().decode("utf-8-sig")


def write_text(path, text, bom):
    with open(path, "wb") as f:
        f.write((b"\xef\xbb\xbf" if bom else b"") + text.encode("utf-8"))


# ---------- install / uninstall ----------

def state_path(exe_dir, *parts):
    return os.path.join(exe_dir, STATE_DIR, *parts)


def load_manifest(exe_dir):
    try:
        with open(state_path(exe_dir, MANIFEST), encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def payload(pkg, route, arch, imports):
    """(source, name) pairs the route puts next to the exe. Direct3D games use Proton's own WineD3D: the
    package's WineD3D DLLs are Wine builds, which Wine takes for its builtins and then fails to load from
    the game folder (ARM64 Proton, Shank: "d3d9.dll not found")."""
    files = [(os.path.join(pkg, "ogl3d", arch, "opengl32.dll"), "opengl32.dll")]
    for src, n in files:
        if not os.path.isfile(src):
            raise SystemExit(T("설치 파일이 없습니다: ", "Missing package file: ") + src)
    return files


def launch_options(route, extra=None, dlls=None):
    s = 'WINEDLLOVERRIDES="%s=n,b" ' % ",".join(dlls or ["opengl32"])
    if route in ("d3d9", "d3d11"):
        s = "PROTON_USE_WINED3D=1 " + s
    s += "%command%"
    if extra:
        s += " " + extra
    return s


# ---------- Steam launch options through the client's DevTools (SharedJSContext) ----------

def devtools_ports():
    """DroidDeck starts Steam with -devtools-port N (guest processes only); Steam with
    .cef-enable-remote-debugging (Steam Deck) listens on 8080."""
    ports = []
    for d in glob.glob("/proc/[0-9]*/cmdline"):
        try:
            with open(d, "rb") as f:
                args = f.read().split(b"\0")
        except OSError:
            continue
        if b"-devtools-port" in args:
            i = args.index(b"-devtools-port")
            if i + 1 < len(args) and args[i + 1].isdigit():
                ports.append(int(args[i + 1]))
    return list(dict.fromkeys(ports + [8080]))


def _ws_eval(port, path, expression, timeout=10):
    import base64
    import socket
    sock = socket.create_connection(("127.0.0.1", port), timeout=timeout)
    try:
        key = base64.b64encode(os.urandom(16)).decode()
        sock.sendall(("GET %s HTTP/1.1\r\nHost: 127.0.0.1:%d\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n"
                      "Sec-WebSocket-Key: %s\r\nSec-WebSocket-Version: 13\r\n\r\n" % (path, port, key)).encode())
        buf = b""
        while b"\r\n\r\n" not in buf:
            chunk = sock.recv(65536)
            if not chunk:
                raise OSError("closed")
            buf += chunk
        head, _, buf = buf.partition(b"\r\n\r\n")
        if not head.startswith(b"HTTP/1.1 101"):
            raise OSError("websocket refused")

        def take(n):
            nonlocal buf
            while len(buf) < n:
                chunk = sock.recv(65536)
                if not chunk:
                    raise OSError("closed")
                buf += chunk
            data, buf = buf[:n], buf[n:]
            return data

        def send(payload, opcode=1):
            mask = os.urandom(4)
            n = len(payload)
            head = (struct.pack("!BB", 0x80 | opcode, 0x80 | n) if n < 126 else
                    struct.pack("!BBH", 0x80 | opcode, 0xFE, n) if n < 65536 else
                    struct.pack("!BBQ", 0x80 | opcode, 0xFF, n))
            sock.sendall(head + mask + bytes(b ^ mask[i % 4] for i, b in enumerate(payload)))

        send(json.dumps({"id": 1, "method": "Runtime.evaluate", "params": {
            "expression": expression, "awaitPromise": True, "returnByValue": True}}).encode())
        message = b""
        while True:
            first, second = take(2)
            n = second & 0x7F
            n = struct.unpack("!H", take(2))[0] if n == 126 else struct.unpack("!Q", take(8))[0] if n == 127 else n
            mask = take(4) if second & 0x80 else None
            payload = take(n)
            if mask:
                payload = bytes(b ^ mask[i % 4] for i, b in enumerate(payload))
            opcode = first & 0x0F
            if opcode == 8:
                raise OSError("closed")
            if opcode == 9:
                send(payload, 10)
                continue
            if opcode == 10:
                continue
            message += payload
            if not first & 0x80:
                continue
            m = json.loads(message.decode("utf-8", "replace"))
            message = b""
            if m.get("id") == 1:
                r = m.get("result") or {}
                if "exceptionDetails" in r or "error" in m:
                    raise OSError("script failed")
                return (r.get("result") or {}).get("value")
    finally:
        sock.close()


def steam_eval(expression):
    """Evaluates expression in the running Steam client; raises OSError when Steam cannot be reached."""
    from urllib.request import urlopen
    for port in devtools_ports():
        try:
            with urlopen("http://127.0.0.1:%d/json/list" % port, timeout=3) as r:
                targets = json.loads(r.read().decode("utf-8", "replace"))
        except (OSError, ValueError):
            continue
        t = next((t for t in targets if t.get("title") == "SharedJSContext" and t.get("webSocketDebuggerUrl")), None)
        if t:
            return _ws_eval(port, "/" + t["webSocketDebuggerUrl"].split("/", 3)[3], expression)
    raise OSError("Steam's DevTools not reachable")


def get_launch_options(appid):
    return steam_eval("new Promise(r => { const u = SteamClient.Apps.RegisterForAppDetails(%d, d => "
                      "{ u.unregister(); r(d.strLaunchOptions || ''); }); setTimeout(() => r(null), 5000); })" % int(appid))


def set_launch_options(appid, options):
    steam_eval("SteamClient.Apps.SetAppLaunchOptions(%d, %s)" % (int(appid), json.dumps(options)))
    return get_launch_options(appid) == options


def apply_launch_options(appid, ours, man, old=None):
    """Sets our launch options in Steam when the game's are empty (or ones this tool set before).
    man["steam"] records it, so removing clears them again."""
    if not appid or not str(appid).isdigit():
        return "manual"
    try:
        cur = get_launch_options(appid)
        if cur is None:
            return "manual"
        if cur.strip() and cur.strip() not in (ours, old):
            man["steam"] = {"appid": appid, "kept": cur}
            return "kept"
        if cur.strip() == ours or set_launch_options(appid, ours):
            man["steam"] = {"appid": appid, "set": ours}
            return "set"
    except (OSError, ValueError):
        pass
    return "manual"


def appid_for_dir(game_dir):
    """The Steam app id whose appmanifest installs into game_dir (steamapps/common/<installdir>)."""
    common = os.path.dirname(os.path.realpath(game_dir))
    if os.path.basename(common) != "common":
        return ""
    for acf in glob.glob(os.path.join(os.path.dirname(common), "appmanifest_*.acf")):
        try:
            with open(acf, encoding="utf-8", errors="replace") as f:
                kv = dict(re.findall(r'"(appid|installdir)"\s+"([^"]*)"', f.read()))
        except OSError:
            continue
        if kv.get("installdir", "").lower() == os.path.basename(game_dir).lower():
            return kv.get("appid", "")
    return ""


def install(exe, pkg, prof, cand, output=None, separation=None, log=print, appid=None):
    exe_dir = os.path.dirname(exe)
    arch, imports = pe_info(exe)
    if arch not in ("x86", "x64"):
        raise SystemExit(T("exe의 32/64비트를 읽지 못했습니다: ", "Could not read whether the exe is 32- or 64-bit: ") + exe)
    route, why = detect_route(exe, prof, cand)
    if route is None:
        raise SystemExit(T("이 게임은 Flato3D로 할 수 없습니다: ", "Flato3D cannot do this game: ") + why)
    rec = recommend(exe, prof, cand)
    output = output or rec["output"]
    separation_given = separation is not None
    separation = rec["separation"] if separation is None else separation

    kept = None
    old_launch = ((load_manifest(exe_dir) or {}).get("steam") or {}).get("set")
    if load_manifest(exe_dir):
        # values tuned in game and saved with Ctrl+F7 stay; only the output (and a separation given here) change
        ini = os.path.join(exe_dir, "OGL3D.ini")
        kept = read_text(ini) if os.path.isfile(ini) else None
        log(T("이미 설치되어 있어 먼저 제거 후 다시 설치합니다 (게임에서 저장한 OGL3D.ini 값은 유지).",
              "Already installed: removing it first (OGL3D.ini values saved in game stay)."))
        uninstall(exe_dir, log=lambda *_: None, clear_launch=False)

    files = payload(pkg, route, arch, imports)
    backup = state_path(exe_dir, "backup")
    os.makedirs(backup, exist_ok=True)
    man = {"tool": VERSION, "time": time.strftime("%Y-%m-%d %H:%M:%S"), "exe": os.path.basename(exe),
           "route": route, "arch": arch, "output": output, "separation": separation,
           "created": [], "backed_up": [], "game_config": [],
           "preexisting": sorted(set(sum((glob.glob(os.path.join(exe_dir, g)) for g in RUNTIME_GLOBS), [])))}
    names = {n for _, n in files} | {"OGL3D.ini"}

    # other tools' graphics DLLs go aside (they would be loaded instead of, or next to, ours)
    for n in CONFLICTS + ["OGL3D.ini"]:
        f = os.path.join(exe_dir, n)
        if os.path.isfile(f):
            shutil.copy2(f, os.path.join(backup, n))
            man["backed_up"].append(n)
            if n not in names:
                os.remove(f)
                log(T("다른 도구의 파일을 옮김: ", "moved another tool's file aside: ") + n)
    for src, n in files:
        dst = os.path.join(exe_dir, n)
        if n not in man["backed_up"]:
            man["created"].append(n)
        if os.path.exists(dst):
            os.chmod(dst, 0o644)
        shutil.copy2(src, dst)

    # OGL3D.ini from the template, with what the Windows installer writes
    if kept is not None:
        text = kept
        if separation_given:
            text = ini_set(text, "Stereo", "separation", fmt(separation))
    else:
        text = read_text(os.path.join(pkg, "ogl3d", "OGL3D.ini"))
        text = ini_set(text, "Flat", "enable", "1" if rec["flat"] else "0")
        text = ini_set(text, "Flat", "rule", rec["rule"])
        text = ini_set(text, "Flat", "depth_screen", "0.3")
        if route != "opengl":
            text = ini_set(text, "Display", "swap_interval", "1")
        if is_unity(exe):
            text = ini_set(text, "Flat", "group", "draw")
            text = ini_set(text, "Flat", "order_curve", "0.5")
            text = ini_set(text, "Flat", "screen_batch", "0")
        for sec, kv in ((prof or {}).get("ini") or {}).items():
            for k, v in kv.items():
                text = ini_set(text, sec, k, fmt(v))
        text = ini_set(text, "Stereo", "separation", fmt(separation))
    text = ini_set(text, "Stereo", "output", output)
    # the upscale presenter (GL -> D3D11 interop) is NVIDIA-only: the game draws at the display's resolution
    text = ini_set(text, "Display", "upscale", "0")
    if "OGL3D.ini" not in man["backed_up"]:
        man["created"].append("OGL3D.ini")
    write_text(os.path.join(exe_dir, "OGL3D.ini"), text, bom=True)

    # the game's own settings (profile game_config; AGS: acsetup.cfg driver=OGL, render_at_screenres=1)
    cfgs = list((prof or {}).get("game_config") or [])
    if not cfgs and os.path.isfile(os.path.join(exe_dir, "acsetup.cfg")):
        cfgs = [{"file": "acsetup.cfg", "section": "graphics", "key": k, "value": v}
                for k, v in (("driver", "OGL"), ("render_at_screenres", "1"))]
    for c in cfgs:
        f = os.path.join(exe_dir, c["file"].replace("\\", "/"))
        if not os.path.isfile(f):
            continue
        t = read_text(f)
        old = ini_get(t, c["section"], c["key"])
        if old == str(c["value"]):
            continue
        man["game_config"].append({"file": c["file"], "section": c["section"], "key": c["key"], "old": old})
        write_text(f, cfg_set(t, c["section"], c["key"], str(c["value"])), bom=False)
        log(T("게임 설정: ", "game setting: ") + "%s [%s] %s = %s" % (c["file"], c["section"], c["key"], c["value"]))

    dlls = ["opengl32"] + [os.path.splitext(n)[0] for _, n in files if n != "opengl32.dll"]
    man["launch_options"] = launch_options(route, (prof or {}).get("launch_options"), dlls)
    man["registry_skipped"] = [r.get("key") for r in (prof or {}).get("registry") or []]
    if not appid:
        d = exe_dir
        for _ in range(4):
            appid = appid_for_dir(d)
            if appid:
                break
            d = os.path.dirname(d)
    appid = appid or (prof or {}).get("steam_appid") or ""
    man["launch_set"] = apply_launch_options(str(appid), man["launch_options"], man, old_launch)
    with open(state_path(exe_dir, MANIFEST), "w", encoding="utf-8") as f:
        json.dump(man, f, indent=1, ensure_ascii=False)
    return {"exe": exe, "arch": arch, "route": route, "why": why, "rule": rec["rule"], "flat": rec["flat"]}, man


def uninstall(exe_dir, log=print, clear_launch=True):
    m = load_manifest(exe_dir)
    if not m:
        raise SystemExit(T("이 도구로 설치한 기록이 없습니다: ", "Nothing installed by this tool in: ") + exe_dir)
    for rel in m["created"]:
        try:
            os.remove(os.path.join(exe_dir, rel))
        except FileNotFoundError:
            pass
    backup = state_path(exe_dir, "backup")
    for rel in m["backed_up"]:
        src = os.path.join(backup, rel)
        if os.path.exists(src):
            dst = os.path.join(exe_dir, rel)
            if os.path.exists(dst):
                os.chmod(dst, 0o644)
            shutil.copy2(src, dst)
    for c in reversed(m.get("game_config", [])):
        f = os.path.join(exe_dir, c["file"].replace("\\", "/"))
        if os.path.isfile(f) and c.get("old") is not None:
            write_text(f, cfg_set(read_text(f), c["section"], c["key"], c["old"]), bom=False)
    pre = set(m.get("preexisting", []))
    for g in RUNTIME_GLOBS:
        for p in glob.glob(os.path.join(exe_dir, g)):
            if p not in pre:
                os.remove(p)
    shutil.rmtree(state_path(exe_dir), ignore_errors=True)
    log(T("제거했습니다: ", "Removed from: ") + exe_dir)
    steam, cleared = m.get("steam") or {}, False
    if steam.get("set") and clear_launch:
        try:
            cleared = get_launch_options(steam["appid"]) != steam["set"] or set_launch_options(steam["appid"], "")
        except (OSError, ValueError):
            pass
    if steam.get("set") and not clear_launch:
        return
    if cleared:
        log(T("Steam 실행 옵션도 지웠습니다.", "Cleared the Steam launch options too."))
    else:
        log(T("Steam 실행 옵션에서 넣었던 문구도 지우세요.", "Also clear the launch options you set in Steam."))


# ---------- user interface ----------

def copy_to_clipboard(text):
    for cmd in (["wl-copy"], ["xclip", "-selection", "clipboard"], ["xsel", "--clipboard", "--input"]):
        if shutil.which(cmd[0]):
            try:
                # wl-copy stays behind to serve the clipboard: it must not hold our output open
                subprocess.run(cmd, input=text.encode(), check=True, timeout=5,
                               stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
                return True
            except (subprocess.SubprocessError, OSError):
                pass
    return False


ROUTE_NAME = {"opengl": "OpenGL", "d3d9": "Direct3D 8/9 -> Proton WineD3D", "d3d11": "Direct3D 10/11 -> Proton WineD3D"}


def report_install(info, man):
    print()
    print(T("Flato3D 설치 완료", "Flato3D installed") + ": " + os.path.dirname(info["exe"]))
    print("  exe: %s (%s), %s (%s)" % (man["exe"], info["arch"], ROUTE_NAME[info["route"]], info["why"]))
    print("  %s: %s, separation %s, output %s" % (T("깊이", "depth"), info["rule"] if info["flat"] else "off",
                                                  fmt(man["separation"]), man["output"]))
    print()
    state = man.get("launch_set")
    if state == "set":
        print(T("1) Steam 실행 옵션을 넣었습니다:", "1) Set the Steam launch options:"))
        print("   " + man["launch_options"])
    else:
        if state == "kept":
            print(T("1) 이 게임에 이미 다른 실행 옵션이 있어 그대로 두었습니다: ", "1) The game already has other launch options; they were kept: ")
                  + man["steam"]["kept"])
            print(T("   Steam > 게임 속성 > 실행 옵션을 다음으로 바꾸세요 (필요한 옵션은 합쳐서):",
                    "   Change Steam > game Properties > Launch Options to this (merge what you need):"))
        else:
            print(T("1) Steam > 게임 속성 > 실행 옵션에 다음을 넣으세요:", "1) Put this in Steam > game Properties > Launch Options:"))
        print("   " + man["launch_options"])
        if copy_to_clipboard(man["launch_options"]):
            print(T("   (클립보드에 복사했습니다)", "   (copied to the clipboard)"))
    print(T("2) DroidDeck: Cardboard를 끄고 봅니다 (게임이 직접 좌우 SBS로 나옴).",
            "2) DroidDeck: keep Cardboard off (the game itself shows side by side)."))
    print(T("   단축키: Ctrl+T 3D 켜기/끄기, Ctrl+F3/F4 분리, Ctrl+F5/F6 화면 위치, Ctrl+F7 저장",
            "   Keys: Ctrl+T 3D on/off, Ctrl+F3/F4 separation, Ctrl+F5/F6 screen plane, Ctrl+F7 save"))
    if man.get("registry_skipped"):
        print(T("   참고: 프로필의 레지스트리 설정은 Linux에서 하지 않았습니다: ", "   Note: the profile's registry settings were not applied on Linux: ")
              + ", ".join(k for k in man["registry_skipped"] if k))
    print(T("   화면이 안 나오면 [제거]로 되돌리세요.", "   If the game shows nothing, [Remove] puts it back."))


def ask(prompt, default=""):
    try:
        v = input(prompt).strip()
    except EOFError:
        sys.exit(0)
    return v or default


def status_label(prof, cand, exe_dir):
    if exe_dir and load_manifest(exe_dir):
        return "[Flato3D] "
    if prof:
        return "(Windows: %s) " % (prof.get("status") or T("프로필", "profile"))
    return ""


def game_rows(args, profiles, candidates):
    rows = []
    for g in steam_games(steam_roots(args.steam_root)):
        prof, cand = match(g, profiles, candidates)
        exes = find_exes(g, prof)
        exe = exes[0] if exes else None
        route, why = detect_route(exe, prof, cand) if exe else (None, T("exe 없음", "no exe"))
        rows.append({"game": g, "prof": prof, "cand": cand, "exes": exes, "exe": exe, "route": route, "why": why})
    return rows


def interactive(args, pkg, profiles, candidates):
    show_all = False
    while True:
        rows = game_rows(args, profiles, candidates)
        shown = [r for r in rows if show_all or r["route"] or (r["exe"] and load_manifest(os.path.dirname(r["exe"])))]
        print()
        print("Flato3D for Linux %s  (%s)" % (VERSION, T("평면 2D 게임 -> 좌우 SBS", "flat 2D games -> side by side")))
        print(T("패키지: ", "Package: ") + pkg)
        if not shown:
            print(T("Flato3D로 할 수 있는 Steam 게임을 찾지 못했습니다.", "No Steam games Flato3D can do were found."))
        for i, r in enumerate(shown, 1):
            route = ROUTE_NAME.get(r["route"], "-").split(" ")[0] if r["route"] else T("불가", "no")
            print("  %3d) %s%s  [%s]" % (i, status_label(r["prof"], r["cand"], r["exe"] and os.path.dirname(r["exe"])),
                                        r["game"]["name"], route))
        hidden = len(rows) - len(shown)
        if hidden and not show_all:
            print(T("  (할 수 없는 게임 %d개 숨김: a 입력 시 모두 보기)", "  (%d games it cannot do are hidden: a shows all)") % hidden)
        v = ask(T("\n게임 번호, exe 경로, a(모두 보기), q(종료): ", "\nGame number, exe path, a (show all), or q to quit: "))
        if v.lower() in ("q", "quit", "exit", ""):
            return
        if v.lower() == "a":
            show_all = not show_all
            continue
        if v.isdigit() and 1 <= int(v) <= len(shown):
            r = shown[int(v) - 1]
            exe = pick_exe(r)
            if exe:
                game_menu(exe, pkg, r["prof"], r["cand"], r["game"]["name"])
        elif os.path.isfile(v):
            g = game_for_exe(v, profiles)
            prof, cand = match(g, profiles, candidates)
            game_menu(v, pkg, prof, cand, g["name"])
        else:
            print(T("잘못된 입력입니다.", "Not understood."))


def pick_exe(row):
    exes = row["exes"]
    if not exes:
        print(T("exe를 찾지 못했습니다.", "No exe found."))
        return None
    if len(exes) == 1:
        return exes[0]
    d = row["game"]["dir"]
    for i, e in enumerate(exes[:15], 1):
        print("  %2d) %s" % (i, os.path.relpath(e, d)))
    v = ask(T("exe 번호 [1]: ", "exe number [1]: "), "1")
    return exes[int(v) - 1] if v.isdigit() and 1 <= int(v) <= min(15, len(exes)) else None


def game_menu(exe, pkg, prof, cand, name):
    exe_dir = os.path.dirname(exe)
    arch, _ = pe_info(exe)
    route, why = detect_route(exe, prof, cand)
    rec = recommend(exe, prof, cand)
    print()
    print(name + "  -  " + exe)
    print("  %s, %s (%s)" % (arch or "?", ROUTE_NAME.get(route, T("할 수 없음", "cannot do")), why))
    if prof:
        print(T("  프로필: ", "  Profile: ") + "%s, %s" % (prof.get("status", "-"), prof.get("verified_by_user", "")))
    print(T("  권장: 깊이 %s, 분리 %s, 출력 %s", "  Suggested: depth %s, separation %s, output %s")
          % (rec["rule"] if rec["flat"] else "off", fmt(rec["separation"]), rec["output"]))
    ours = load_manifest(exe_dir)
    if ours:
        print(T("  이 도구로 설치됨 (%s, %s)", "  Installed by this tool (%s, %s)") % (ours["time"], ours["output"]))
    print(T("  1) 설치 (권장값)   2) 출력/분리 골라서 설치   3) 제거   4) 실행 옵션 보기   0) 뒤로",
            "  1) Install (suggested)   2) Install with output/separation   3) Remove   4) Show launch options   0) Back"))
    v = ask("> ", "0")
    try:
        if v == "1":
            report_install(*install(exe, pkg, prof, cand))
        elif v == "2":
            for i, o in enumerate(OUTPUTS, 1):
                print("  %d) %s" % (i, {"sbs": T("좌우 SBS", "side by side"), "tab": T("위아래", "top and bottom")}[o]))
            c = ask("[1]: ", "1")
            output = OUTPUTS[int(c) - 1] if c.isdigit() and 1 <= int(c) <= len(OUTPUTS) else "sbs"
            s = ask(T("분리 [%s]: ", "separation [%s]: ") % fmt(rec["separation"]), fmt(rec["separation"]))
            try:
                sep = float(s)
            except ValueError:
                sep = rec["separation"]
            report_install(*install(exe, pkg, prof, cand, output=output, separation=sep))
        elif v == "3":
            uninstall(exe_dir)
        elif v == "4":
            print("  " + (ours or {}).get("launch_options", launch_options(route, (prof or {}).get("launch_options"))))
    except SystemExit as e:
        print("!! %s" % e)


def game_for_exe(exe, profiles):
    """The game folder an exe path belongs to: under steamapps/common, a folder named in a profile, else the exe's."""
    exe = os.path.realpath(exe)
    d = os.path.dirname(exe)
    up = [d]
    for _ in range(3):
        up.append(os.path.dirname(up[-1]))
    for a in up:
        if os.path.basename(os.path.dirname(a)) == "common":
            return {"appid": "", "name": os.path.basename(a), "dir": a}
    for a in up:
        p = next((p for p in profiles if (p.get("folder") or "").lower() == os.path.basename(a).lower()), None)
        if p and os.path.realpath(os.path.join(a, (p.get("exe") or "").replace("\\", "/"))) == exe:
            return {"appid": "", "name": p.get("name") or os.path.basename(a), "dir": a}
    return {"appid": "", "name": os.path.basename(d), "dir": d}


def resolve(target, args, profiles, candidates):
    if os.path.isfile(target):
        return (target,) + match(game_for_exe(target, profiles), profiles, candidates)
    for r in game_rows(args, profiles, candidates):
        if r["game"]["appid"] == target or r["game"]["name"].lower() == target.lower():
            if not r["exe"]:
                raise SystemExit(T("exe를 찾지 못했습니다: ", "No exe found: ") + r["game"]["dir"])
            return r["exe"], r["prof"], r["cand"]
    raise SystemExit(T("게임을 찾지 못했습니다: ", "Game not found: ") + target)


def main():
    ap = argparse.ArgumentParser(description="Install Flato3D (stereo depth for flat 2D games) into Steam/Proton games.")
    ap.add_argument("--package", help="Flato3D-SR package folder (ogl3d/, wined3d/, data/)")
    ap.add_argument("--steam-root", action="append", default=[], help="extra Steam library root (repeatable)")
    ap.add_argument("--list", action="store_true", help="list Steam games, the route and the Flato3D state")
    ap.add_argument("--install", metavar="EXE|APPID|NAME")
    ap.add_argument("--uninstall", metavar="EXE|APPID|NAME")
    ap.add_argument("--output", choices=OUTPUTS)
    ap.add_argument("--separation", type=float)
    args = ap.parse_args()

    pkg = package_dir(args.package)
    if not pkg:
        raise SystemExit(T("Flato3D-SR 패키지 폴더를 찾지 못했습니다. 이 스크립트를 Flato3D-SR 폴더 안(linux/)에 두거나 --package로 지정하세요.",
                           "The Flato3D-SR package was not found. Put this script in the Flato3D-SR folder (linux/) or pass --package."))
    profiles, candidates = load_data(pkg)
    if args.uninstall:
        exe, _, _ = resolve(args.uninstall, args, profiles, candidates)
        uninstall(os.path.dirname(exe))
    elif args.list:
        for r in game_rows(args, profiles, candidates):
            state = "flato3d" if r["exe"] and load_manifest(os.path.dirname(r["exe"])) else "-"
            print("%-8s %-8s %-7s %-9s %s" % (r["game"]["appid"], state, r["route"] or "no",
                                             (r["prof"] or {}).get("status", "-"), r["game"]["name"]))
    elif args.install:
        exe, prof, cand = resolve(args.install, args, profiles, candidates)
        report_install(*install(exe, pkg, prof, cand, args.output, args.separation))
    else:
        interactive(args, pkg, profiles, candidates)


if __name__ == "__main__":
    main()
