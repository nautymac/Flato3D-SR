# suspended.py <pid>: threads of a running process that are suspended by someone else right now (a garbage
# collector's stop-the-world, a debugger, a watchdog), with where they stopped. SuspendThread returns the previous
# suspend count; this tool suspends each thread for a moment only to read it (and its RIP/EIP) and resumes it.
# A thread frozen inside RtlLeaveCriticalSection / heap code can leave a lock in a state its waiters never leave.
import ctypes
import os
import struct
import sys
from ctypes import wintypes

pid = int(sys.argv[1])
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
psapi = ctypes.WinDLL('psapi', use_last_error=True)
k32.OpenProcess.restype = wintypes.HANDLE
k32.OpenThread.restype = wintypes.HANDLE
k32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
k32.SuspendThread.restype = wintypes.DWORD
k32.Wow64SuspendThread.restype = wintypes.DWORD
hp = k32.OpenProcess(0x0400 | 0x0010, False, pid)
wow = wintypes.BOOL()
k32.IsWow64Process(hp, ctypes.byref(wow))


class THREADENTRY32(ctypes.Structure):
    _fields_ = [('dwSize', wintypes.DWORD), ('cntUsage', wintypes.DWORD), ('th32ThreadID', wintypes.DWORD),
                ('th32OwnerProcessID', wintypes.DWORD), ('tpBasePri', wintypes.LONG), ('tpDeltaPri', wintypes.LONG), ('dwFlags', wintypes.DWORD)]


class MODULEINFO(ctypes.Structure):
    _fields_ = [('lpBaseOfDll', ctypes.c_void_p), ('SizeOfImage', wintypes.DWORD), ('EntryPoint', ctypes.c_void_p)]


mods = (wintypes.HMODULE * 2048)()
need = wintypes.DWORD()
psapi.EnumProcessModulesEx(hp, mods, ctypes.sizeof(mods), ctypes.byref(need), 3)
ranges = []
for m in mods[:need.value // ctypes.sizeof(wintypes.HMODULE)]:
    name = ctypes.create_unicode_buffer(260)
    psapi.GetModuleBaseNameW(hp, wintypes.HMODULE(m), name, 260)
    mi = MODULEINFO()
    psapi.GetModuleInformation(hp, wintypes.HMODULE(m), ctypes.byref(mi), ctypes.sizeof(mi))
    ranges.append((mi.lpBaseOfDll, mi.lpBaseOfDll + mi.SizeOfImage, name.value))


def where(addr):
    for lo, hi, n in ranges:
        if lo <= addr < hi:
            return '%s+0x%x' % (n, addr - lo)
    return '0x%x' % addr


snap = k32.CreateToolhelp32Snapshot(0x4, 0)
te = THREADENTRY32()
te.dwSize = ctypes.sizeof(te)
tids = []
ok = k32.Thread32First(snap, ctypes.byref(te))
while ok:
    if te.th32OwnerProcessID == pid:
        tids.append(te.th32ThreadID)
    ok = k32.Thread32Next(snap, ctypes.byref(te))

raw = ctypes.create_string_buffer(1232 + 16)
ctx = (ctypes.addressof(raw) + 15) & ~15
frozen = 0
for tid in tids:
    ht = k32.OpenThread(0x0002 | 0x0008 | 0x0040, False, tid)
    if not ht:
        continue
    prev = k32.SuspendThread(ht)
    ip = None
    if prev not in (0, 0xFFFFFFFF) and not wow.value:
        ctypes.memset(ctx, 0, 1232)
        struct.pack_into('<I', (ctypes.c_char * 1232).from_address(ctx), 0x30, 0x00100001)   # CONTEXT_CONTROL
        if k32.GetThreadContext(ht, ctypes.c_void_p(ctx)):
            ip = struct.unpack_from('<Q', ctypes.string_at(ctx, 1232), 0xF8)[0]
    k32.ResumeThread(ht)
    k32.CloseHandle(ht)
    if prev not in (0, 0xFFFFFFFF):
        frozen += 1
        print('thread %d suspended %d time(s) by someone else%s' % (tid, prev, ', at ' + where(ip) if ip else ''))
print('%d of %d threads suspended by someone else' % (frozen, len(tids)))
