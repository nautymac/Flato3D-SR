# peexp.py <dll> <hex offsets...>: nearest exported symbol at or below each RVA
import struct, sys
d = open(sys.argv[1], 'rb').read()
pe = struct.unpack_from('<I', d, 0x3c)[0]
nsec = struct.unpack_from('<H', d, pe + 6)[0]; optsz = struct.unpack_from('<H', d, pe + 20)[0]
opt = pe + 24; magic = struct.unpack_from('<H', d, opt)[0]
dd = opt + (96 if magic == 0x10b else 112)
exp_rva = struct.unpack_from('<I', d, dd)[0]
secs = [struct.unpack_from('<IIII', d, opt + optsz + i * 40 + 8) for i in range(nsec)]
def off(r):
    for vs, va, rs, ro in secs:
        if va <= r < va + max(vs, rs): return r - va + ro
o = off(exp_rva)
nfun, nnam, afun, anam, aord = struct.unpack_from('<IIIII', d, o + 20)
funs = [struct.unpack_from('<I', d, off(afun) + 4 * i)[0] for i in range(nfun)]
syms = []
for i in range(nnam):
    nr = struct.unpack_from('<I', d, off(anam) + 4 * i)[0]
    ordi = struct.unpack_from('<H', d, off(aord) + 2 * i)[0]
    no = off(nr); name = d[no:d.index(b'\0', no)].decode()
    syms.append((funs[ordi], name))
syms.sort()
for h in sys.argv[2:]:
    r = int(h, 16); best = None
    for a, n in syms:
        if a <= r: best = (a, n)
    print(h, best[1] if best else '?', hex(r - best[0]) if best else '')
