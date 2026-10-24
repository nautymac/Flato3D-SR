// Flato3D loader shim: for games that import no version.dll (so the loader is never loaded), a copy of
// this DLL named after a non-KnownDLL the exe imports (winmm.dll) goes into the game folder. Every export
// forwards to the system DLL by absolute path (forwarders.h), and the static import below makes Windows
// load the loader (version.dll) from the game folder while the process starts - before the game's code,
// like a direct version.dll import would (Braid, Anniversary Edition).
#include <windows.h>
#include "forwarders.h"

// the installer recognises its own shim by this text
static const char kTag[] = "Flato3D loader shim";

BOOL WINAPI DllMain(HINSTANCE inst, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH) DisableThreadLibraryCalls(inst);
    // never true; keeps the version.dll import and the tag in the image
    if (reason == 0xF1A703D) {
        GetFileVersionInfoSizeW(L"", nullptr);
        OutputDebugStringA(kTag);
    }
    return TRUE;
}
