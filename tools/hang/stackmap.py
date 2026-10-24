# stackmap.py <stacks.txt> --gamedir <game folder> [--thread TID ...] [--match REGEX] [--dll name=path ...]
#             [--map name=path.map ...] [--frames N]
# Names the frames of a stackdump output (module+offset) with symutil: system DLLs from their PDB (getpdb.ps1),
# MinGW builds with symbols, MSVC map files, else the nearest export. Modules are looked up in --gamedir, then
# System32; --dll / --map override. Without --thread, prints the threads whose frames match --match (default:
# threads that are not in a plain wait, plus any thread inside wined3d/opengl32/d3d11 copies).
# Note: stackdump scans the stack for code addresses, so stale return addresses from earlier calls can appear.
import argparse
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import symutil  # noqa: E402

ap = argparse.ArgumentParser()
ap.add_argument('stacks')
ap.add_argument('--gamedir', default='.')
ap.add_argument('--thread', type=int, action='append', default=[])
ap.add_argument('--match')
ap.add_argument('--dll', action='append', default=[])
ap.add_argument('--map', action='append', default=[])
ap.add_argument('--frames', type=int, default=30)
a = ap.parse_args()

blocks, cur = [], None
for line in open(a.stacks, encoding='utf-8', errors='replace'):
    line = line.rstrip()
    m = re.match(r'thread (\d+)\s+ip (\S+)', line)
    if m:
        cur = {'tid': int(m.group(1)), 'ip': m.group(2), 'frames': []}
        blocks.append(cur)
    elif cur is not None and line.strip():
        cur['frames'].append(line.strip())

mods = {re.match(r'(.+?)\+0x', f).group(1).lower() for b in blocks for f in [b['ip']] + b['frames'] if re.match(r'.+?\+0x', f)}
paths = {}
sys32 = os.path.join(os.environ['SystemRoot'], 'System32')
for mod in mods:
    for d in (a.gamedir, sys32):
        p = os.path.join(d, mod)
        if os.path.exists(p):
            paths[mod] = p
            break
paths.update(dict(x.split('=', 1) for x in a.dll))
namer = symutil.Namer(paths, dict(x.split('=', 1) for x in a.map))


def name(frame):
    m = re.match(r'(.+?)\+0x([0-9a-fA-F]+)$', frame)
    if not m:
        return frame
    n = namer.name(m.group(1), int(m.group(2), 16))
    return '%s  (%s)' % (frame, n) if n else frame


plain_wait = re.compile(r'ntdll\.dll\+0x16[0-9a-f]{4}$')
default = re.compile(r'wined3d|w3d11|wxgi|opengl32', re.I)
for b in blocks:
    text = ' '.join(b['frames'])
    if a.thread:
        if b['tid'] not in a.thread:
            continue
    elif a.match:   # raw frames and their names (e.g. 'Heap|CriticalSection')
        named = ' '.join(name(f) for f in [b['ip']] + b['frames'][:a.frames])
        if not re.search(a.match, named, re.I):
            continue
    elif plain_wait.search(b['ip']) and not default.search(text):
        continue
    print('thread %d  ip %s' % (b['tid'], name(b['ip'])))
    for f in b['frames'][:a.frames]:
        print('    ' + name(f))
    print()
