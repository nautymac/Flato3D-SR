# Makes WineD3D's D3D10/11 DLLs loadable under names that do not clash with the system ones:
# d3d11.dll -> w3d11.dll, dxgi.dll -> wxgi.dll, d3d10core.dll -> w3d10core.dll (import names
# patched in place, same length). The NVIDIA OpenGL driver presents through the system DXGI/D3D11
# and, when a module called d3d11.dll is already loaded, it would get WineD3D's and deadlock.
# The Flato3D loader sends the game's requests for d3d11.dll/dxgi.dll to the w* copies.
#
#   python -I wined3d_rename.py <wined3d folder> <output folder>
import os
import shutil
import struct
import sys

RENAME = {'d3d11.dll': 'w3d11.dll', 'dxgi.dll': 'wxgi.dll', 'd3d10core.dll': 'w3d10core.dll'}


def patch_imports(data: bytearray):
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    nsec = struct.unpack_from('<H', data, pe + 6)[0]
    opt = pe + 24
    optsz = struct.unpack_from('<H', data, pe + 20)[0]
    magic = struct.unpack_from('<H', data, opt)[0]
    dd = opt + (96 if magic == 0x10b else 112)
    imp_rva = struct.unpack_from('<I', data, dd + 8)[0]
    secs = []
    for i in range(nsec):
        o = opt + optsz + i * 40
        secs.append(struct.unpack_from('<III', data, o + 8))   # vsize, va, rawsize ... (va at +12)
    secs = [(struct.unpack_from('<I', data, opt + optsz + i * 40 + 12)[0],
             struct.unpack_from('<I', data, opt + optsz + i * 40 + 8)[0],
             struct.unpack_from('<I', data, opt + optsz + i * 40 + 20)[0]) for i in range(nsec)]

    def off(rva):
        for va, vs, raw in secs:
            if va <= rva < va + max(vs, 1):
                return rva - va + raw
        raise ValueError(hex(rva))

    o = off(imp_rva)
    changed = []
    while True:
        name_rva = struct.unpack_from('<I', data, o + 12)[0]
        if name_rva == 0:
            break
        no = off(name_rva)
        end = data.index(b'\0', no)
        name = data[no:end].decode().lower()
        if name in RENAME:
            new = RENAME[name].encode()
            assert len(new) == end - no
            data[no:end] = new
            changed.append(f'{name}->{RENAME[name]}')
        o += 20
    return changed


def main(src, dst):
    os.makedirs(dst, exist_ok=True)
    for f in os.listdir(src):
        if not f.lower().endswith('.dll'):
            continue
        data = bytearray(open(os.path.join(src, f), 'rb').read())
        changed = patch_imports(data)
        out = RENAME.get(f.lower(), f)
        if f.lower() in RENAME or changed:
            open(os.path.join(dst, out), 'wb').write(data)
            print(f'{f} -> {out}  imports: {", ".join(changed) or "-"}')
        else:
            shutil.copy2(os.path.join(src, f), os.path.join(dst, out))


if __name__ == '__main__':
    main(sys.argv[1], sys.argv[2])
