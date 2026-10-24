# csscan.py <pid> <module>...: critical sections held right now inside the given modules' images (static locks such
# as WineD3D's global wined3d_cs). Held = RecursionCount >= 1, LockCount != -1, and an OwningThread that is a live
# thread of the process. Locks on the heap (per-object locks) need cslist.py or regs.py instead.
import ctypes
import struct
import sys
from ctypes import wintypes

pid = int(sys.argv[1])
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
psapi = ctypes.WinDLL('psapi', use_last_error=True)
k32.OpenProcess.restype = wintypes.HANDLE
k32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
hp = k32.OpenProcess(0x0400 | 0x0010, False, pid)
assert hp, 'OpenProcess failed'


class THREADENTRY32(ctypes.Structure):
    _fields_ = [('dwSize', wintypes.DWORD), ('cntUsage', wintypes.DWORD), ('th32ThreadID', wintypes.DWORD),
                ('th32OwnerProcessID', wintypes.DWORD), ('tpBasePri', wintypes.LONG), ('tpDeltaPri', wintypes.LONG), ('dwFlags', wintypes.DWORD)]


class MODULEINFO(ctypes.Structure):
    _fields_ = [('lpBaseOfDll', ctypes.c_void_p), ('SizeOfImage', wintypes.DWORD), ('EntryPoint', ctypes.c_void_p)]


snap = k32.CreateToolhelp32Snapshot(0x4, 0)
te = THREADENTRY32()
te.dwSize = ctypes.sizeof(te)
tids = set()
ok = k32.Thread32First(snap, ctypes.byref(te))
while ok:
    if te.th32OwnerProcessID == pid:
        tids.add(te.th32ThreadID)
    ok = k32.Thread32Next(snap, ctypes.byref(te))

mods = (wintypes.HMODULE * 2048)()
need = wintypes.DWORD()
psapi.EnumProcessModulesEx(hp, mods, ctypes.sizeof(mods), ctypes.byref(need), 3)
wanted = {m.lower() for m in sys.argv[2:]}
for m in mods[:need.value // ctypes.sizeof(wintypes.HMODULE)]:
    name = ctypes.create_unicode_buffer(260)
    psapi.GetModuleBaseNameW(hp, wintypes.HMODULE(m), name, 260)
    if name.value.lower() not in wanted:
        continue
    mi = MODULEINFO()
    psapi.GetModuleInformation(hp, wintypes.HMODULE(m), ctypes.byref(mi), ctypes.sizeof(mi))
    found = 0
    for page in range(0, mi.SizeOfImage, 0x1000):
        buf = ctypes.create_string_buffer(0x1000 + 40)
        got = ctypes.c_size_t()
        if not k32.ReadProcessMemory(hp, ctypes.c_void_p(mi.lpBaseOfDll + page), buf, 0x1000 + 40, ctypes.byref(got)) or got.value < 0x1000:
            continue
        data = buf.raw[:got.value]
        for o in range(0, 0x1000, 8):
            if o + 40 > len(data):
                break
            dbg, lockcount, recursion, owner, sem, spin = struct.unpack_from('<QiiQQQ', data, o)
            if 1 <= recursion < 1000 and owner in tids and -100000 < lockcount < -1:
                print('%s+0x%x: LockCount %d Recursion %d OwningThread %d DebugInfo 0x%x' % (name.value, page + o, lockcount, recursion, owner, dbg))
                found += 1
    print('%s: %d held critical section(s)' % (name.value, found))
print('threads:', len(tids))
