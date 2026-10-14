# memstr.py <pid> <out.txt> <needle>...: read-only scan of a process's committed memory for byte strings;
# writes the printable strings around each hit
import ctypes, ctypes.wintypes as W, sys, re
pid = int(sys.argv[1]); out = open(sys.argv[2], 'w', encoding='utf-8')
needles = [n.encode() for n in sys.argv[3:]]
k = ctypes.WinDLL('kernel32', use_last_error=True)
h = k.OpenProcess(0x0400 | 0x0010, False, pid)
class MBI(ctypes.Structure):
    _fields_ = [('BaseAddress', ctypes.c_void_p), ('AllocationBase', ctypes.c_void_p), ('AllocationProtect', W.DWORD),
                ('RegionSize', ctypes.c_size_t), ('State', W.DWORD), ('Protect', W.DWORD), ('Type', W.DWORD)]
k.VirtualQueryEx.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.POINTER(MBI), ctypes.c_size_t]
k.ReadProcessMemory.argtypes = [W.HANDLE, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_size_t, ctypes.POINTER(ctypes.c_size_t)]
addr = 0; m = MBI(); hits = 0
while addr < 0x7fff0000 and k.VirtualQueryEx(h, addr, ctypes.byref(m), ctypes.sizeof(m)):
    base = m.BaseAddress or 0
    if m.State == 0x1000 and m.Type == 0x1000000 and not (m.Protect & 0x100) and m.RegionSize < 64 << 20:   # MEM_IMAGE
        buf = ctypes.create_string_buffer(m.RegionSize); n = ctypes.c_size_t()
        if k.ReadProcessMemory(h, base, buf, m.RegionSize, ctypes.byref(n)):
            data = buf.raw[:n.value]
            for nd in needles:
                for mt in re.finditer(re.escape(nd), data):
                    hits += 1
                    lo = max(0, mt.start() - 600); hi = mt.end() + 600
                    ctx = re.findall(rb'[\x20-\x7e]{4,}', data[lo:hi])
                    print(f'== {nd!r} at {base + mt.start():#x}', file=out)
                    print('  ' + ' | '.join(c.decode() for c in ctx), file=out)
    addr = base + m.RegionSize
print('hits', hits, file=out)
