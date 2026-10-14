// Flato3D loader: a version.dll proxy that makes games use the graphics wrappers placed next to
// them (WineD3D, dgVoodoo, ...), even when they ask for the system copy by full path, restrict the
// DLL search to System32, or never call LoadLibrary/GetProcAddress where we could see it.
//
//  * Loaded early: version.dll is not a KnownDLL, so a copy in the game folder wins for every
//    module that imports it (SDL2.dll does). Exports forward to the real System32 version.dll.
//  * The loader imports (LoadLibrary*, GetModuleHandle*) of the game's own modules (the exe and DLLs
//    in its folder, not the wrappers) are patched: a request for one of kRedirect gets the copy in
//    the game folder when there is one.
//  * When a system copy of one of kRedirect gets mapped anyway (Steam loads d3d9.dll early; Door
//    Kickers: Action Squad's SteamStub-wrapped exe then reaches it without any import we see), its
//    creation entry points are bent to stubs that call the game-folder copy (LdrRegisterDllNotification).
//  * Flato3D_loader_verbose.txt next to the exe: log every LoadLibrary call and graphics lookup.
#include <windows.h>
#include <psapi.h>
#include <algorithm>
#include <cstdio>
#include <cwchar>
#include <intrin.h>
#include <string>
#include <vector>

namespace {

HMODULE g_self, g_real;
std::wstring g_dir;   // game folder (the exe's), with trailing backslash
FILE* g_log;
bool g_verbose;
CRITICAL_SECTION g_logLock;

const wchar_t* const kRedirect[] = {L"d3d8.dll",  L"d3d9.dll",      L"ddraw.dll", L"d3d10.dll", L"d3d10_1.dll",
                                    L"d3d10core.dll", L"d3d11.dll", L"dxgi.dll"};
// modules in the game folder whose imports stay untouched (the wrappers load system DLLs on purpose)
const wchar_t* const kLeaveAlone[] = {L"opengl32.dll", L"wined3d.dll", L"version.dll"};

std::wstring BaseName(const std::wstring& p);

void Log(const char* fmt, ...)
{
    EnterCriticalSection(&g_logLock);
    if (!g_log) {
        // one log per process (a crash handler next to the game would overwrite the game's)
        wchar_t exe[MAX_PATH];
        GetModuleFileNameW(nullptr, exe, MAX_PATH);
        std::wstring name = BaseName(exe);
        name = name.substr(0, name.find_last_of(L'.'));
        g_log = _wfopen((g_dir + L"Flato3D_loader_" + name + L".log").c_str(), L"w");
    }
    if (g_log) {
        SYSTEMTIME t;
        GetLocalTime(&t);
        fprintf(g_log, "%02d:%02d:%02d.%03d ", t.wHour, t.wMinute, t.wSecond, t.wMilliseconds);
        va_list ap;
        va_start(ap, fmt);
        vfprintf(g_log, fmt, ap);
        va_end(ap);
        fputc('\n', g_log);
        fflush(g_log);
    }
    LeaveCriticalSection(&g_logLock);
}

std::wstring BaseName(const std::wstring& p)
{
    size_t s = p.find_last_of(L"\\/");
    return s == std::wstring::npos ? p : p.substr(s + 1);
}

bool InList(const std::wstring& name, const wchar_t* const* list, size_t n)
{
    for (size_t i = 0; i < n; ++i)
        if (_wcsicmp(name.c_str(), list[i]) == 0) return true;
    return false;
}

bool InGameDir(const std::wstring& path)
{
    return path.size() > g_dir.size() && _wcsnicmp(path.c_str(), g_dir.c_str(), g_dir.size()) == 0;
}

// The game-folder copy to load instead, or empty.
std::wstring Redirect(LPCWSTR request)
{
    if (!request || (uintptr_t)request < 0x10000) return {};
    std::wstring req = request, base = BaseName(req);
    if (base.find(L'.') == std::wstring::npos) base += L".dll";
    if (!InList(base, kRedirect, _countof(kRedirect))) return {};
    // A renamed copy (w3d11.dll for d3d11.dll, wxgi.dll for dxgi.dll; tools/wined3d_rename.py) wins:
    // it keeps the system names free for the GPU driver and overlays loaded into the same process.
    std::wstring local = g_dir + L"w" + base.substr(1);
    if (GetFileAttributesW(local.c_str()) == INVALID_FILE_ATTRIBUTES) local = g_dir + base;
    if (GetFileAttributesW(local.c_str()) == INVALID_FILE_ATTRIBUTES) return {};
    if (_wcsicmp(req.c_str(), local.c_str()) == 0) return {};
    return local;
}

std::wstring Widen(LPCSTR s)
{
    if (!s || (uintptr_t)s < 0x10000) return {};
    int n = MultiByteToWideChar(CP_ACP, 0, s, -1, nullptr, 0);
    std::wstring w(n > 0 ? n - 1 : 0, L'\0');
    if (n > 1) MultiByteToWideChar(CP_ACP, 0, s, -1, &w[0], n);
    return w;
}

std::string Caller(void* ret)
{
    char who[MAX_PATH] = "?";
    HMODULE m = nullptr;
    GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, (LPCSTR)ret, &m);
    if (m) GetModuleFileNameA(m, who, MAX_PATH);
    const char* slash = strrchr(who, '\\');
    char buf[MAX_PATH + 16];
    snprintf(buf, sizeof(buf), "%s+0x%x", slash ? slash + 1 : who, (unsigned)((BYTE*)ret - (BYTE*)m));
    return buf;
}

void PatchGameModules();

HMODULE LoadRedirected(const std::wstring& local, const std::wstring& original)
{
    // full path + altered search path: the wrapper's own dependencies (wined3d.dll) come from its folder
    HMODULE m = LoadLibraryExW(local.c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
    Log("redirected %ls -> %ls (%p)", original.c_str(), local.c_str(), (void*)m);
    return m;
}

HMODULE LoadW(LPCWSTR name, HANDLE file, DWORD flags, void* ret)
{
    if (g_verbose) Log("LoadLibrary(%ls, 0x%x) from %s", (uintptr_t)name > 0xFFFF ? name : L"#", flags, Caller(ret).c_str());
    std::wstring local = Redirect(name);
    HMODULE m = !local.empty() ? LoadRedirected(local, name) : LoadLibraryExW(name, file, flags);
    PatchGameModules();
    return m;
}

HMODULE LoadA(LPCSTR name, HANDLE file, DWORD flags, void* ret)
{
    std::wstring w = Widen(name);
    if (g_verbose) Log("LoadLibrary(%ls, 0x%x) from %s", w.c_str(), flags, Caller(ret).c_str());
    std::wstring local = Redirect(w.c_str());
    HMODULE m = !local.empty() ? LoadRedirected(local, w) : LoadLibraryExA(name, file, flags);
    PatchGameModules();
    return m;
}

HMODULE WINAPI HookLoadLibraryExW(LPCWSTR name, HANDLE file, DWORD flags) { return LoadW(name, file, flags, _ReturnAddress()); }
HMODULE WINAPI HookLoadLibraryW(LPCWSTR name) { return LoadW(name, nullptr, 0, _ReturnAddress()); }
HMODULE WINAPI HookLoadLibraryExA(LPCSTR name, HANDLE file, DWORD flags) { return LoadA(name, file, flags, _ReturnAddress()); }
HMODULE WINAPI HookLoadLibraryA(LPCSTR name) { return LoadA(name, nullptr, 0, _ReturnAddress()); }

// GetModuleHandle("d3d9.dll") returns whichever copy was loaded first; Steam loads the system one
// early, so a game asking by name would use it: answer with the game-folder copy once it is loaded.
HMODULE LocalLoaded(LPCWSTR name)
{
    std::wstring local = Redirect(name);
    return local.empty() ? nullptr : GetModuleHandleW(local.c_str());
}

HMODULE WINAPI HookGetModuleHandleW(LPCWSTR name)
{
    if (HMODULE m = LocalLoaded(name)) return m;
    return GetModuleHandleW(name);
}

HMODULE WINAPI HookGetModuleHandleA(LPCSTR name)
{
    std::wstring w = Widen(name);
    if (HMODULE m = !w.empty() ? LocalLoaded(w.c_str()) : nullptr) return m;
    return GetModuleHandleA(name);
}

BOOL WINAPI HookGetModuleHandleExW(DWORD flags, LPCWSTR name, HMODULE* out)
{
    if (!(flags & GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS) && out) {
        if (HMODULE m = LocalLoaded(name)) {
            *out = m;
            if (!(flags & GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT)) {
                HMODULE again;
                GetModuleHandleExW(0, Redirect(name).c_str(), &again);   // takes the reference
            }
            return TRUE;
        }
    }
    return GetModuleHandleExW(flags, name, out);
}

FARPROC WINAPI HookGetProcAddress(HMODULE m, LPCSTR name)
{
    FARPROC f = GetProcAddress(m, name);
    if (g_verbose) {
        wchar_t path[MAX_PATH] = L"?";
        GetModuleFileNameW(m, path, MAX_PATH);
        std::wstring base = BaseName(path);
        bool gfx = InList(base, kRedirect, _countof(kRedirect)) || _wcsnicmp(base.c_str(), L"d3dx", 4) == 0;
        if (gfx || ((uintptr_t)name > 0xFFFF && (strstr(name, "3D") || strstr(name, "wgl"))))
            Log("GetProcAddress(%ls, %s) = %p", path, (uintptr_t)name > 0xFFFF ? name : "#ordinal", (void*)f);
    }
    return f;
}

struct Hook {
    const char* name;
    void* fn;
};
const Hook kHooks[] = {
    {"LoadLibraryW", (void*)HookLoadLibraryW},
    {"LoadLibraryA", (void*)HookLoadLibraryA},
    {"LoadLibraryExW", (void*)HookLoadLibraryExW},
    {"LoadLibraryExA", (void*)HookLoadLibraryExA},
    {"GetModuleHandleW", (void*)HookGetModuleHandleW},
    {"GetModuleHandleA", (void*)HookGetModuleHandleA},
    {"GetModuleHandleExW", (void*)HookGetModuleHandleExW},
    {"GetProcAddress", (void*)HookGetProcAddress},
};

bool IsLoaderImport(const char* dll)
{
    return _stricmp(dll, "kernel32.dll") == 0 || _stricmp(dll, "kernelbase.dll") == 0 ||
           _strnicmp(dll, "api-ms-win-core-libraryloader", 29) == 0 || _strnicmp(dll, "api-ms-win-core-kernel32", 24) == 0;
}

int PatchImports(HMODULE m)
{
    auto base = (BYTE*)m;
    auto dos = (IMAGE_DOS_HEADER*)base;
    if (dos->e_magic != IMAGE_DOS_SIGNATURE) return 0;
    auto nt = (IMAGE_NT_HEADERS*)(base + dos->e_lfanew);
    const IMAGE_DATA_DIRECTORY& dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT];
    if (!dir.VirtualAddress) return 0;
    int patched = 0;
    for (auto imp = (IMAGE_IMPORT_DESCRIPTOR*)(base + dir.VirtualAddress); imp->Name; ++imp) {
        if (!IsLoaderImport((const char*)(base + imp->Name)) || !imp->OriginalFirstThunk) continue;
        auto names = (IMAGE_THUNK_DATA*)(base + imp->OriginalFirstThunk);
        auto slots = (IMAGE_THUNK_DATA*)(base + imp->FirstThunk);
        for (; names->u1.AddressOfData; ++names, ++slots) {
            if (IMAGE_SNAP_BY_ORDINAL(names->u1.Ordinal)) continue;
            auto byName = (IMAGE_IMPORT_BY_NAME*)(base + names->u1.AddressOfData);
            for (const Hook& h : kHooks) {
                if (strcmp((const char*)byName->Name, h.name) != 0 || slots->u1.Function == (ULONG_PTR)h.fn) continue;
                DWORD old;
                if (VirtualProtect(&slots->u1.Function, sizeof(void*), PAGE_READWRITE, &old)) {
                    slots->u1.Function = (ULONG_PTR)h.fn;
                    VirtualProtect(&slots->u1.Function, sizeof(void*), old, &old);
                    ++patched;
                }
            }
        }
    }
    return patched;
}

// ---------------------------------------------------------------------------------------------
// System copies mapped anyway: their creation entry points jump to stubs that call the game-folder
// copy, which is loaded on the first call (outside the loader lock).

FARPROC LocalProc(const wchar_t* dll, const char* name, void* ret)
{
    HMODULE m = LoadLibraryExW((g_dir + dll).c_str(), nullptr, LOAD_WITH_ALTERED_SEARCH_PATH);
    FARPROC f = m ? GetProcAddress(m, name) : nullptr;
    Log("%ls!%s called from %s -> game folder copy %p", dll, name, Caller(ret).c_str(), (void*)f);
    return f;
}

#define STUB(ret, dll, name, params, args)                                                 \
    ret WINAPI Stub_##name params                                                          \
    {                                                                                      \
        static auto s_fn = (ret(WINAPI*) params)LocalProc(dll, #name, _ReturnAddress());   \
        return s_fn ? s_fn args : (ret)(LONG_PTR)0x80004005L;                              \
    }

STUB(void*, L"d3d9.dll", Direct3DCreate9, (UINT v), (v))
STUB(HRESULT, L"d3d9.dll", Direct3DCreate9Ex, (UINT v, void** out), (v, out))
STUB(void*, L"d3d8.dll", Direct3DCreate8, (UINT v), (v))
STUB(HRESULT, L"ddraw.dll", DirectDrawCreate, (GUID* g, void** out, void* outer), (g, out, outer))
STUB(HRESULT, L"ddraw.dll", DirectDrawCreateEx, (GUID* g, void** out, const GUID* iid, void* outer), (g, out, iid, outer))

struct Entry {
    const wchar_t* dll;
    const char* name;
    void* stub;
};
const Entry kEntries[] = {
    {L"d3d9.dll", "Direct3DCreate9", (void*)Stub_Direct3DCreate9},
    {L"d3d9.dll", "Direct3DCreate9Ex", (void*)Stub_Direct3DCreate9Ex},
    {L"d3d8.dll", "Direct3DCreate8", (void*)Stub_Direct3DCreate8},
    {L"ddraw.dll", "DirectDrawCreate", (void*)Stub_DirectDrawCreate},
    {L"ddraw.dll", "DirectDrawCreateEx", (void*)Stub_DirectDrawCreateEx},
    // d3d11/dxgi are left alone: the NVIDIA OpenGL driver presents through the system DXGI/D3D11
    // (OpenGL on a DXGI swap chain), and bending them sent the driver's own device into WineD3D.
    // D3D11 games reach the wrapper through the patched LoadLibrary/GetProcAddress imports.
};

bool WriteJump(void* from, void* to)
{
#ifdef _WIN64
    BYTE code[14] = {0xFF, 0x25, 0, 0, 0, 0};   // jmp [rip+0]; dq target
    memcpy(code + 6, &to, 8);
#else
    BYTE code[5] = {0xE9};
    LONG rel = (LONG)((BYTE*)to - ((BYTE*)from + 5));
    memcpy(code + 1, &rel, 4);
#endif
    DWORD old;
    if (!VirtualProtect(from, sizeof(code), PAGE_EXECUTE_READWRITE, &old)) return false;
    memcpy(from, code, sizeof(code));
    VirtualProtect(from, sizeof(code), old, &old);
    FlushInstructionCache(GetCurrentProcess(), from, sizeof(code));
    return true;
}

// Export lookup without GetProcAddress (safe inside the DLL notification, before the DLL is initialised).
void* FindExport(BYTE* base, const char* name)
{
    auto dos = (IMAGE_DOS_HEADER*)base;
    auto nt = (IMAGE_NT_HEADERS*)(base + dos->e_lfanew);
    const IMAGE_DATA_DIRECTORY& dir = nt->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_EXPORT];
    if (!dir.VirtualAddress) return nullptr;
    auto exp = (IMAGE_EXPORT_DIRECTORY*)(base + dir.VirtualAddress);
    auto names = (DWORD*)(base + exp->AddressOfNames);
    auto ords = (WORD*)(base + exp->AddressOfNameOrdinals);
    auto funcs = (DWORD*)(base + exp->AddressOfFunctions);
    for (DWORD i = 0; i < exp->NumberOfNames; ++i) {
        if (strcmp((const char*)(base + names[i]), name) != 0) continue;
        DWORD rva = funcs[ords[i]];
        if (rva >= dir.VirtualAddress && rva < dir.VirtualAddress + dir.Size) return nullptr;   // forwarder
        return base + rva;
    }
    return nullptr;
}

std::vector<void*> g_bent;   // system DLL bases already bent

void BendSystemDll(void* sysBase, const std::wstring& base)
{
    for (void* b : g_bent)
        if (b == sysBase) return;
    g_bent.push_back(sysBase);
    if (GetFileAttributesW((g_dir + base).c_str()) == INVALID_FILE_ATTRIBUTES) return;
    for (const Entry& e : kEntries) {
        if (_wcsicmp(e.dll, base.c_str()) != 0) continue;
        void* from = FindExport((BYTE*)sysBase, e.name);
        if (!from) continue;
        bool ok = WriteJump(from, e.stub);
        Log("system %ls!%s -> game folder copy: %s", base.c_str(), e.name, ok ? "ok" : "failed");
    }
}

// LdrRegisterDllNotification (ntdll, documented): called while a DLL is being mapped
struct UStr {
    USHORT Length, MaximumLength;
    PWSTR Buffer;
};
struct DllNotifyData {
    ULONG Flags;
    const UStr* FullDllName;
    const UStr* BaseDllName;
    PVOID DllBase;
    ULONG SizeOfImage;
};
using DllNotify_t = VOID(CALLBACK*)(ULONG reason, const DllNotifyData* data, PVOID ctx);
using LdrRegisterDllNotification_t = LONG(NTAPI*)(ULONG flags, DllNotify_t fn, PVOID ctx, PVOID* cookie);

VOID CALLBACK OnDllNotify(ULONG reason, const DllNotifyData* d, PVOID)
{
    if (reason != 1 || !d || !d->FullDllName || !d->BaseDllName) return;   // 1 = loaded
    std::wstring full(d->FullDllName->Buffer, d->FullDllName->Length / 2);
    std::wstring base(d->BaseDllName->Buffer, d->BaseDllName->Length / 2);
    if (!InList(base, kRedirect, _countof(kRedirect)) || InGameDir(full)) return;
    Log("system %ls is being loaded", full.c_str());
    BendSystemDll(d->DllBase, base);
}

void RegisterDllNotify()
{
    auto reg = (LdrRegisterDllNotification_t)GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "LdrRegisterDllNotification");
    PVOID cookie = nullptr;
    LONG st = reg ? reg(0, OnDllNotify, nullptr, &cookie) : -1;
    Log("DLL load notification %s", st == 0 ? "registered" : "unavailable");
}

// ---------------------------------------------------------------------------------------------

std::vector<HMODULE> g_seen;

void PatchGameModules()
{
    HMODULE mods[1024];
    DWORD need = 0;
    if (!K32EnumProcessModules(GetCurrentProcess(), mods, sizeof(mods), &need)) return;
    size_t n = std::min<size_t>(need / sizeof(HMODULE), _countof(mods));
    for (size_t i = 0; i < n; ++i) {
        HMODULE m = mods[i];
        if (m == g_self || std::find(g_seen.begin(), g_seen.end(), m) != g_seen.end()) continue;
        wchar_t path[MAX_PATH];
        if (!GetModuleFileNameW(m, path, MAX_PATH)) continue;
        g_seen.push_back(m);
        std::wstring p = path, base = BaseName(p);
        if (!InGameDir(p)) {
            if (InList(base, kRedirect, _countof(kRedirect))) {
                Log("system %ls was already loaded", p.c_str());
                BendSystemDll(m, base);
            }
            continue;
        }
        if (InList(base, kRedirect, _countof(kRedirect)) || InList(base, kLeaveAlone, _countof(kLeaveAlone))) continue;
        int k = PatchImports(m);
        Log("patched %d loader imports in %ls", k, base.c_str());
    }
}

FARPROC Real(const char* name)
{
    if (!g_real) {
        wchar_t sys[MAX_PATH];
        GetSystemDirectoryW(sys, MAX_PATH);
        g_real = LoadLibraryW((std::wstring(sys) + L"\\version.dll").c_str());
    }
    return g_real ? GetProcAddress(g_real, name) : nullptr;
}

}  // namespace

// ---------------------------------------------------------------------------------------------
// version.dll exports, forwarded to System32

#define FWD(ret, name, params, args)                                  \
    extern "C" ret WINAPI P_##name params                              \
    {                                                                  \
        static auto s_fn = (ret(WINAPI*) params)Real(#name);           \
        if (!s_fn) {                                                   \
            SetLastError(ERROR_PROC_NOT_FOUND);                        \
            return 0;                                                  \
        }                                                              \
        return s_fn args;                                              \
    }

FWD(BOOL, GetFileVersionInfoA, (LPCSTR a, DWORD b, DWORD c, LPVOID d), (a, b, c, d))
FWD(BOOL, GetFileVersionInfoW, (LPCWSTR a, DWORD b, DWORD c, LPVOID d), (a, b, c, d))
FWD(BOOL, GetFileVersionInfoExA, (DWORD f, LPCSTR a, DWORD b, DWORD c, LPVOID d), (f, a, b, c, d))
FWD(BOOL, GetFileVersionInfoExW, (DWORD f, LPCWSTR a, DWORD b, DWORD c, LPVOID d), (f, a, b, c, d))
FWD(DWORD, GetFileVersionInfoSizeA, (LPCSTR a, LPDWORD b), (a, b))
FWD(DWORD, GetFileVersionInfoSizeW, (LPCWSTR a, LPDWORD b), (a, b))
FWD(DWORD, GetFileVersionInfoSizeExA, (DWORD f, LPCSTR a, LPDWORD b), (f, a, b))
FWD(DWORD, GetFileVersionInfoSizeExW, (DWORD f, LPCWSTR a, LPDWORD b), (f, a, b))
// undocumented: (DWORD flags, HANDLE file, LPVOID* data, PDWORD len)
FWD(BOOL, GetFileVersionInfoByHandle, (DWORD a, HANDLE b, LPVOID* c, PDWORD d), (a, b, c, d))
FWD(DWORD, VerFindFileA, (DWORD a, LPCSTR b, LPCSTR c, LPCSTR d, LPSTR e, PUINT f, LPSTR g, PUINT h), (a, b, c, d, e, f, g, h))
FWD(DWORD, VerFindFileW, (DWORD a, LPCWSTR b, LPCWSTR c, LPCWSTR d, LPWSTR e, PUINT f, LPWSTR g, PUINT h), (a, b, c, d, e, f, g, h))
FWD(DWORD, VerInstallFileA, (DWORD a, LPCSTR b, LPCSTR c, LPCSTR d, LPCSTR e, LPCSTR f, LPSTR g, PUINT h), (a, b, c, d, e, f, g, h))
FWD(DWORD, VerInstallFileW, (DWORD a, LPCWSTR b, LPCWSTR c, LPCWSTR d, LPCWSTR e, LPCWSTR f, LPWSTR g, PUINT h), (a, b, c, d, e, f, g, h))
FWD(DWORD, VerLanguageNameA, (DWORD a, LPSTR b, DWORD c), (a, b, c))
FWD(DWORD, VerLanguageNameW, (DWORD a, LPWSTR b, DWORD c), (a, b, c))
FWD(BOOL, VerQueryValueA, (LPCVOID a, LPCSTR b, LPVOID* c, PUINT d), (a, b, c, d))
FWD(BOOL, VerQueryValueW, (LPCVOID a, LPCWSTR b, LPVOID* c, PUINT d), (a, b, c, d))

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) {
        g_self = inst;
        DisableThreadLibraryCalls(inst);
        InitializeCriticalSection(&g_logLock);
        wchar_t exe[MAX_PATH];
        GetModuleFileNameW(nullptr, exe, MAX_PATH);
        g_dir = exe;
        g_dir.resize(g_dir.find_last_of(L"\\/") + 1);
        g_verbose = GetFileAttributesW((g_dir + L"Flato3D_loader_verbose.txt").c_str()) != INVALID_FILE_ATTRIBUTES;
        Log("Flato3D loader in %ls", exe);
        RegisterDllNotify();
        PatchGameModules();
    }
    return TRUE;
}
