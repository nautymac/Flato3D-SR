// stackdump <pid|exe name>: prints every thread of a (hung) process with the code addresses found on
// its stack, as module+offset. Runs outside the target, so it does not need the target's loader or
// heap locks. Build it with the target's bitness (x86 for 32-bit games).
#include <windows.h>
#include <psapi.h>
#include <tlhelp32.h>
#include <cstdio>
#include <string>
#include <vector>

struct Mod {
    ULONG_PTR base, size;
    std::string name;
};

static std::string Describe(const std::vector<Mod>& mods, ULONG_PTR a)
{
    for (const Mod& m : mods)
        if (a >= m.base && a < m.base + m.size) {
            char buf[300];
            snprintf(buf, sizeof(buf), "%s+0x%llx", m.name.c_str(), (unsigned long long)(a - m.base));
            return buf;
        }
    return {};
}

int wmain(int argc, wchar_t** argv)
{
    if (argc < 2) {
        fprintf(stderr, "usage: stackdump <pid|exe name>\n");
        return 1;
    }
    DWORD pid = wcstoul(argv[1], nullptr, 10);
    if (!pid) {
        HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        PROCESSENTRY32W pe{sizeof(pe)};
        for (BOOL ok = Process32FirstW(snap, &pe); ok; ok = Process32NextW(snap, &pe))
            if (_wcsicmp(pe.szExeFile, argv[1]) == 0) pid = pe.th32ProcessID;
        CloseHandle(snap);
    }
    HANDLE proc = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, FALSE, pid);
    if (!proc) {
        fprintf(stderr, "cannot open process %lu\n", pid);
        return 1;
    }
    std::vector<Mod> mods;
    HMODULE hm[2048];
    DWORD need = 0;
    if (EnumProcessModulesEx(proc, hm, sizeof(hm), &need, LIST_MODULES_ALL)) {
        for (DWORD i = 0; i < need / sizeof(HMODULE) && i < 2048; ++i) {
            MODULEINFO mi{};
            char name[MAX_PATH] = "?";
            GetModuleInformation(proc, hm[i], &mi, sizeof(mi));
            GetModuleBaseNameA(proc, hm[i], name, MAX_PATH);
            mods.push_back({(ULONG_PTR)mi.lpBaseOfDll, mi.SizeOfImage, name});
        }
    }
    HANDLE snap = CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD, 0);
    THREADENTRY32 te{sizeof(te)};
    for (BOOL ok = Thread32First(snap, &te); ok; ok = Thread32Next(snap, &te)) {
        if (te.th32OwnerProcessID != pid) continue;
        HANDLE t = OpenThread(THREAD_SUSPEND_RESUME | THREAD_GET_CONTEXT | THREAD_QUERY_INFORMATION, FALSE, te.th32ThreadID);
        if (!t) continue;
        SuspendThread(t);
        CONTEXT ctx{};
        ctx.ContextFlags = CONTEXT_CONTROL | CONTEXT_INTEGER;
        GetThreadContext(t, &ctx);
#ifdef _WIN64
        ULONG_PTR ip = ctx.Rip, sp = ctx.Rsp;
#else
        ULONG_PTR ip = ctx.Eip, sp = ctx.Esp;
#endif
        std::vector<ULONG_PTR> words(4096);
        SIZE_T got = 0;
        ReadProcessMemory(proc, (void*)sp, words.data(), words.size() * sizeof(ULONG_PTR), &got);
        ResumeThread(t);
        CloseHandle(t);
        printf("thread %lu  ip %s\n", te.th32ThreadID, Describe(mods, ip).c_str());
        int shown = 0;
        for (size_t i = 0; i < got / sizeof(ULONG_PTR) && shown < 24; ++i) {
            std::string d = Describe(mods, words[i]);
            if (d.empty()) continue;
            printf("    %s\n", d.c_str());
            ++shown;
        }
    }
    CloseHandle(snap);
    CloseHandle(proc);
    return 0;
}
