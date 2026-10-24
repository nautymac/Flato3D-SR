# cslist.py <pid>: walks ntdll!RtlCriticalSectionList in a running x64 process and prints held critical sections,
# and any whose debug record carries a name (Wine stores one in DebugInfo->Spare[0], e.g.
# "wined3d_device_gl.allocator_cs"). Only locks created with a debug record are in that list (Wine forces one).
# Needs ntdll's PDB (getpdb.ps1). LockCount: -1 free; bit 0 clear = held; bit 1 clear = a waiter was woken;
# (-1 - LockCount) >> 2 = waiting threads.
import ctypes
import os
import struct
import sys
from ctypes import wintypes

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import symutil  # noqa: E402

pid = int(sys.argv[1])
k32 = ctypes.WinDLL('kernel32', use_last_error=True)
k32.OpenProcess.restype = wintypes.HANDLE
k32.GetModuleHandleW.restype = ctypes.c_void_p
hp = k32.OpenProcess(0x0400 | 0x0010, False, pid)
assert hp, 'OpenProcess failed'


def read(addr, n):
    buf = ctypes.create_string_buffer(n)
    got = ctypes.c_size_t()
    return buf.raw if k32.ReadProcessMemory(hp, ctypes.c_void_p(addr), buf, n, ctypes.byref(got)) else None


def cstr(addr):
    b = read(addr, 64)
    s = b.split(b'\0')[0] if b else b''
    return s.decode('latin-1') if s and all(32 <= c < 127 for c in s) else None


ntdll = os.path.join(os.environ['SystemRoot'], 'System32', 'ntdll.dll')
ntbase = k32.GetModuleHandleW('ntdll.dll')   # system DLLs share their base across processes
found = symutil.PdbSymbols(ntdll).lookup('RtlCriticalSectionList')
assert found, 'RtlCriticalSectionList not in the PDB'
head = ntbase + found[0]
flink = struct.unpack('<Q', read(head, 8))[0]
n = held = 0
while flink and flink != head and n < 200000:
    n += 1
    raw = read(flink - 0x10, 0x30)   # RTL_CRITICAL_SECTION_DEBUG: ProcessLocksList at +0x10
    if not raw:
        break
    cs = struct.unpack_from('<Q', raw, 8)[0]
    csraw = read(cs, 40) if cs else None
    if csraw:
        lockcount, recursion, owner = struct.unpack_from('<iiQ', csraw, 8)
        name = None
        for off in (0x28, 0x2c):
            p = struct.unpack_from('<Q', raw + b'\0' * 8, off)[0]
            name = cstr(p) if 0x10000 < p < 0x7fffffffffff else None
            if name:
                break
        if owner or name:
            held += owner != 0
            print('cs 0x%x %-50s LockCount %d Recursion %d OwningThread %d' % (cs, name or '?', lockcount, recursion, owner))
    flink = struct.unpack('<Q', raw[0x10:0x18])[0]
print('%d critical sections with debug records, %d held' % (n, held))
