"""Names for code addresses (module + offset), from whatever a DLL offers:

- Microsoft system DLLs (ntdll, kernelbase, ...): their PDB, downloaded by getpdb.ps1 into tools\\hang\\sym
  (<sym>\\<pdb name>\\<GUID+age>\\<pdb name>, the symbol-server layout), read with the system dbghelp.dll
- MinGW builds that keep their COFF symbol table (WineD3D 11.0 from fdossena.com does, 11.19 does not)
- MSVC linker map files (our opengl32.dll: ogl3d\\bin\\<arch>\\opengl32.map)
- otherwise the nearest export (internal functions then show as "<export>+<large offset>": a hint only)
"""
import ctypes
import os
import re
import struct
import uuid
from ctypes import wintypes

HERE = os.path.dirname(os.path.abspath(__file__))
SYM = os.path.join(HERE, 'sym')


def pe_sections(d):
    pe = struct.unpack_from('<I', d, 0x3C)[0]
    nsec = struct.unpack_from('<H', d, pe + 6)[0]
    optsz = struct.unpack_from('<H', d, pe + 20)[0]
    secs = []
    for k in range(nsec):
        o = pe + 24 + optsz + 40 * k
        vsize, va, rsize, rptr = struct.unpack_from('<IIII', d, o + 8)
        secs.append((d[o:o + 8].rstrip(b'\0').decode('latin-1'), va, vsize, rsize, rptr))
    return pe, optsz, secs


def rva_to_off(secs, rva):
    for _, va, vs, rs, rp in secs:
        if va <= rva < va + max(vs, rs):
            return rva - va + rp
    return None


def pdb_id(dll):
    """(pdb file name, GUID+age) from the CodeView record, or None."""
    d = open(dll, 'rb').read()
    pe, optsz, secs = pe_sections(d)
    opt = pe + 24
    dd = opt + (112 if struct.unpack_from('<H', d, opt)[0] == 0x20B else 96)
    rva, size = struct.unpack_from('<II', d, dd + 6 * 8)
    o = rva_to_off(secs, rva) if rva else None
    for k in range((size // 28) if o is not None else 0):
        typ = struct.unpack_from('<I', d, o + 28 * k + 12)[0]
        sz, _, ptr = struct.unpack_from('<III', d, o + 28 * k + 16)
        if typ == 2 and d[ptr:ptr + 4] == b'RSDS':
            rec = d[ptr:ptr + sz]
            name = rec[24:rec.index(b'\0', 24)].decode()
            return os.path.basename(name), uuid.UUID(bytes_le=rec[4:20]).hex.upper() + '%X' % struct.unpack_from('<I', rec, 20)[0]
    return None


def pdb_dir(dll):
    """Folder holding the dll's downloaded PDB, or None."""
    pid = pdb_id(dll)
    if not pid:
        return None
    d = os.path.join(SYM, pid[0], pid[1])
    return d if os.path.exists(os.path.join(d, pid[0])) else None


class PdbSymbols:
    """dbghelp session for one DLL whose PDB is in SYM (system dbghelp, no symbol server)."""

    class SYMBOL_INFOW(ctypes.Structure):
        _fields_ = [('SizeOfStruct', wintypes.ULONG), ('TypeIndex', wintypes.ULONG), ('Reserved', ctypes.c_uint64 * 2),
                    ('Index', wintypes.ULONG), ('Size', wintypes.ULONG), ('ModBase', ctypes.c_uint64), ('Flags', wintypes.ULONG),
                    ('Value', ctypes.c_uint64), ('Address', ctypes.c_uint64), ('Register', wintypes.ULONG), ('Scope', wintypes.ULONG),
                    ('Tag', wintypes.ULONG), ('NameLen', wintypes.ULONG), ('MaxNameLen', wintypes.ULONG), ('Name', ctypes.c_wchar * 512)]

    _next = 0x5100

    def __init__(self, dll, base=0x180000000):
        folder = pdb_dir(dll)
        if not folder:
            raise FileNotFoundError('no PDB for %s in %s - run getpdb.ps1 "%s"' % (dll, SYM, dll))
        self.dbg = ctypes.WinDLL(os.path.join(os.environ['SystemRoot'], 'System32', 'dbghelp.dll'), use_last_error=True)
        PdbSymbols._next += 1
        self.h = wintypes.HANDLE(PdbSymbols._next)
        self.base = base
        self.dbg.SymSetOptions(0x2 | 0x80000)
        self.dbg.SymInitializeW.argtypes = [wintypes.HANDLE, wintypes.LPCWSTR, wintypes.BOOL]
        self.dbg.SymInitializeW(self.h, folder, False)
        self.dbg.SymLoadModuleExW.restype = ctypes.c_uint64
        self.dbg.SymLoadModuleExW.argtypes = [wintypes.HANDLE, wintypes.HANDLE, wintypes.LPCWSTR, wintypes.LPCWSTR, ctypes.c_uint64,
                                              wintypes.DWORD, ctypes.c_void_p, wintypes.DWORD]
        self.dbg.SymLoadModuleExW(self.h, None, dll, None, base, 0, None, 0)
        self.dbg.SymFromAddrW.argtypes = [wintypes.HANDLE, ctypes.c_uint64, ctypes.POINTER(ctypes.c_uint64), ctypes.c_void_p]

    def _info(self):
        si = self.SYMBOL_INFOW()
        si.SizeOfStruct = ctypes.sizeof(self.SYMBOL_INFOW) - 1024
        si.MaxNameLen = 512
        return si

    def name(self, rva):
        si, disp = self._info(), ctypes.c_uint64()
        if self.dbg.SymFromAddrW(self.h, ctypes.c_uint64(self.base + rva), ctypes.byref(disp), ctypes.byref(si)):
            return '%s+0x%x' % (si.Name, disp.value)
        return None

    def lookup(self, symbol):
        """(rva, size) of a symbol, or None."""
        si = self._info()
        if self.dbg.SymFromNameW(self.h, symbol, ctypes.byref(si)):
            return si.Address - self.base, si.Size
        return None


def coff_symbols(dll):
    """[(rva, name)] from a MinGW DLL's COFF symbol table ([] when stripped)."""
    d = open(dll, 'rb').read()
    pe, optsz, secs = pe_sections(d)
    symptr, nsyms = struct.unpack_from('<II', d, pe + 12)
    out, i, strtab = [], 0, symptr + 18 * nsyms
    while i < nsyms:
        o = symptr + 18 * i
        raw = d[o:o + 8]
        if raw[:4] == b'\0\0\0\0':
            off = struct.unpack_from('<I', raw, 4)[0]
            name = d[strtab + off:d.index(b'\0', strtab + off)]
        else:
            name = raw.rstrip(b'\0')
        value, secnum, typ, sclass, naux = struct.unpack_from('<IhHBB', d, o + 8)
        if 0 < secnum <= len(secs) and (typ & 0x20 or sclass in (2, 3)):
            out.append((secs[secnum - 1][1] + value, name.decode('latin-1')))
        i += 1 + naux
    return sorted(out)


def map_symbols(mapfile):
    """[(rva, name)] from an MSVC linker map."""
    text = open(mapfile, encoding='latin-1').read()
    base = int(re.search(r'Preferred load address is ([0-9a-fA-F]+)', text).group(1), 16)
    out = [(int(m.group(2), 16) - base, m.group(1)) for m in
           re.finditer(r'^\s*[0-9a-fA-F]{4}:[0-9a-fA-F]{8}\s+(\S+)\s+([0-9a-fA-F]{8,16})\s', text, re.M)]
    return sorted(out)


def export_symbols(dll):
    """[(rva, name)] of a DLL's exports."""
    d = open(dll, 'rb').read()
    pe, optsz, secs = pe_sections(d)
    opt = pe + 24
    dd = opt + (112 if struct.unpack_from('<H', d, opt)[0] == 0x20B else 96)
    rva = struct.unpack_from('<I', d, dd)[0]
    o = rva_to_off(secs, rva) if rva else None
    if o is None:
        return []
    nfun, nnames, afun, aname, aord = struct.unpack_from('<IIIII', d, o + 20)
    funcs = [struct.unpack_from('<I', d, rva_to_off(secs, afun) + 4 * k)[0] for k in range(nfun)]
    out = []
    for k in range(nnames):
        np = struct.unpack_from('<I', d, rva_to_off(secs, aname) + 4 * k)[0]
        ordinal = struct.unpack_from('<H', d, rva_to_off(secs, aord) + 2 * k)[0]
        no = rva_to_off(secs, np)
        out.append((funcs[ordinal], d[no:d.index(b'\0', no)].decode('latin-1') + ' [export]'))
    return sorted(out)


def nearest(table, rva):
    best = None
    lo, hi = 0, len(table)
    while lo < hi:
        mid = (lo + hi) // 2
        if table[mid][0] <= rva:
            best = table[mid]
            lo = mid + 1
        else:
            hi = mid
    return '%s+0x%x' % (best[1], rva - best[0]) if best else None


class Namer:
    """Names module+offset with the best source each module has; sources are loaded once per module."""

    def __init__(self, paths, maps=None):
        self.paths = {k.lower(): v for k, v in paths.items()}   # module name -> file
        self.maps = {k.lower(): v for k, v in (maps or {}).items()}
        self.cache = {}

    def _source(self, mod):
        if mod in self.cache:
            return self.cache[mod]
        src = None
        path = self.paths.get(mod)
        if mod in self.maps:
            src = ('map', map_symbols(self.maps[mod]))
        elif path and os.path.exists(path):
            try:
                src = ('pdb', PdbSymbols(path)) if pdb_dir(path) else None
            except OSError:
                src = None
            if not src:
                coff = coff_symbols(path)
                src = ('coff', coff) if coff else ('exports', export_symbols(path))
        self.cache[mod] = src
        return src

    def name(self, mod, rva):
        src = self._source(mod.lower())
        if not src:
            return None
        return src[1].name(rva) if src[0] == 'pdb' else nearest(src[1], rva)
