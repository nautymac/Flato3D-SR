# regs.py <pid>: for every thread of a running x64 process that sits in ntdll's critical-section wait
# (RtlpWaitOnCriticalSection, found in ntdll's PDB), prints its registers and, for each register pointing at a
# plausible RTL_CRITICAL_SECTION, that lock's state and owner: the lock a thread waits on is in a register, even
# when it lives on the heap and has no debug record. Needs ntdll's PDB (getpdb.ps1).
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
k32.OpenThread.restype = wintypes.HANDLE
k32.CreateToolhelp32Snapshot.restype = wintypes.HANDLE
k32.GetModuleHandleW.restype = ctypes.c_void_p
hp = k32.OpenProcess(0x0400 | 0x0010, False, pid)
ntbase = k32.GetModuleHandleW('ntdll.dll')
ntdll = os.path.join(os.environ['SystemRoot'], 'System32', 'ntdll.dll')
wait = symutil.PdbSymbols(ntdll).lookup('RtlpWaitOnCriticalSection')
assert wait, 'RtlpWaitOnCriticalSection not in the PDB'
lo, hi = ntbase + wait[0], ntbase + wait[0] + max(wait[1], 0x400)


def read(addr, n):
    buf = ctypes.create_string_buffer(n)
    got = ctypes.c_size_t()
    return buf.raw if k32.ReadProcessMemory(hp, ctypes.c_void_p(addr), buf, n, ctypes.byref(got)) else None


class THREADENTRY32(ctypes.Structure):
    _fields_ = [('dwSize', wintypes.DWORD), ('cntUsage', wintypes.DWORD), ('th32ThreadID', wintypes.DWORD),
                ('th32OwnerProcessID', wintypes.DWORD), ('tpBasePri', wintypes.LONG), ('tpDeltaPri', wintypes.LONG), ('dwFlags', wintypes.DWORD)]


snap = k32.CreateToolhelp32Snapshot(0x4, 0)
te = THREADENTRY32()
te.dwSize = ctypes.sizeof(te)
tids = []
ok = k32.Thread32First(snap, ctypes.byref(te))
while ok:
    if te.th32OwnerProcessID == pid:
        tids.append(te.th32ThreadID)
    ok = k32.Thread32Next(snap, ctypes.byref(te))

NAMES = ['rax', 'rcx', 'rdx', 'rbx', 'rsp', 'rbp', 'rsi', 'rdi', 'r8', 'r9', 'r10', 'r11', 'r12', 'r13', 'r14', 'r15']
raw = ctypes.create_string_buffer(1232 + 16)
ctx = (ctypes.addressof(raw) + 15) & ~15   # CONTEXT must be 16-byte aligned
waiting = 0
for tid in tids:
    ht = k32.OpenThread(0x0002 | 0x0008 | 0x0040, False, tid)
    if not ht:
        continue
    k32.SuspendThread(ht)
    ctypes.memset(ctx, 0, 1232)
    struct.pack_into('<I', (ctypes.c_char * 1232).from_address(ctx), 0x30, 0x0010000B)   # CONTEXT_FULL (AMD64)
    got = k32.GetThreadContext(ht, ctypes.c_void_p(ctx))
    k32.ResumeThread(ht)
    k32.CloseHandle(ht)
    if not got:
        continue
    blob = ctypes.string_at(ctx, 1232)
    rip = struct.unpack_from('<Q', blob, 0xF8)[0]
    if not lo <= rip < hi:
        continue
    waiting += 1
    print('thread %d waits on a critical section (rip ntdll+0x%x)' % (tid, rip - ntbase))
    for name, v in zip(NAMES, struct.unpack_from('<16Q', blob, 0x78)):
        note = ''
        cs = read(v, 40) if v > 0x10000 else None
        if cs:
            dbg, lockcount, recursion, owner = struct.unpack_from('<QiiQ', cs)
            if -100000 < lockcount < 0 and 0 <= recursion < 1000 and owner < 0x100000:
                note = '  <- lock? LockCount %d Recursion %d OwningThread %d DebugInfo 0x%x' % (lockcount, recursion, owner, dbg)
        print('  %-3s 0x%016x%s' % (name, v, note))
print('%d thread(s) waiting on a critical section' % waiting)
