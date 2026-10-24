// alert_cs_repro.cpp: shows that one stray thread alert (NtAlertThreadByThreadId) hangs the thread's next
// contended EnterCriticalSection on Windows 11 24H2 and later (found with The Messenger, 2026-10-24).
//
// Since 24H2 a critical-section waiter links a wait block on its own stack into PEB->WaitOnAddressHashTable
// and sleeps in NtWaitForAlertByThreadId. A pending alert makes that sleep return at once; the waiter takes
// it for the releaser's wake-up and links the same, still linked block again, so the block's next pointer is
// the block itself and RtlpWaitOnCriticalSection walks it forever (ip ntdll+0xfc9b on build 26200, rbx =
// [rbx+0x10]). WineD3D sends such alerts: its command stream thread sleeps in NtWaitForAlertByThreadId with a
// timeout, and an alert sent after that wait has already timed out stays pending.
//
// Build (x64 Native Tools prompt): cl /O2 alert_cs_repro.cpp psapi.lib
//   alert_cs_repro 0   no stray alert: the waiter gets the lock
//   alert_cs_repro 1   stray alert first: the waiter is stuck (exit code 3)
#include <windows.h>
#include <psapi.h>
#include <cstdio>
#include <cstdlib>

using AlertFn = LONG(WINAPI*)(HANDLE tid);
static CRITICAL_SECTION g_cs;
static HANDLE g_ready, g_go;

static DWORD WINAPI Waiter(void*)
{
    SetEvent(g_ready);
    WaitForSingleObject(g_go, INFINITE);   // an alert sent now is not consumed by this wait: it stays pending
    EnterCriticalSection(&g_cs);           // contended: the holder keeps it for 500 ms
    LeaveCriticalSection(&g_cs);
    return 0;
}

static DWORD WINAPI Holder(void*)
{
    EnterCriticalSection(&g_cs);
    Sleep(500);
    LeaveCriticalSection(&g_cs);
    return 0;
}

int main(int argc, char** argv)
{
    int stray = argc > 1 ? atoi(argv[1]) : 1;
    auto alert = (AlertFn)GetProcAddress(GetModuleHandleA("ntdll.dll"), "NtAlertThreadByThreadId");
    InitializeCriticalSectionAndSpinCount(&g_cs, 0);
    g_ready = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    g_go = CreateEventW(nullptr, TRUE, FALSE, nullptr);
    DWORD tid;
    HANDLE w = CreateThread(nullptr, 0, Waiter, nullptr, 0, &tid);
    WaitForSingleObject(g_ready, INFINITE);
    CreateThread(nullptr, 0, Holder, nullptr, 0, nullptr);
    Sleep(50);   // the holder owns the lock
    if (stray) alert((HANDLE)(ULONG_PTR)tid);
    SetEvent(g_go);
    if (WaitForSingleObject(w, 5000) == WAIT_OBJECT_0) {
        printf("stray alert %d: the waiter got the lock\n", stray);
        return 0;
    }
    SuspendThread(w);
    CONTEXT c{};
    c.ContextFlags = CONTEXT_FULL;
    GetThreadContext(w, &c);
    MODULEINFO mi{};
    GetModuleInformation(GetCurrentProcess(), GetModuleHandleA("ntdll.dll"), &mi, sizeof(mi));
#if defined(_M_X64)
    printf("stray alert %d: the waiter is STUCK at ntdll+0x%llx (rbx %p, [rbx+0x10] %p)\n", stray,
           (unsigned long long)(c.Rip - (ULONG_PTR)mi.lpBaseOfDll), (void*)c.Rbx, *(void**)(c.Rbx + 0x10));
#else
    printf("stray alert %d: the waiter is STUCK at ntdll+0x%lx\n", stray, (unsigned long)(c.Eip - (ULONG_PTR)mi.lpBaseOfDll));
#endif
    fflush(stdout);
    TerminateProcess(GetCurrentProcess(), 3);   // the stuck thread never leaves
}
