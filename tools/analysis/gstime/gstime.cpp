// gstime <vs> <fs> [gs]: compiles and links the shaders in a GL 4.4 core context and prints the times.
// A random comment is appended to every source so the driver's shader cache cannot answer.
#include <windows.h>
#include <GL/gl.h>
#include <chrono>
#include <cstdio>
#include <fstream>
#include <sstream>
#include <string>

typedef char GLchar;
#define GL_FRAGMENT_SHADER 0x8B30
#define GL_VERTEX_SHADER 0x8B31
#define GL_GEOMETRY_SHADER 0x8DD9
#define GL_COMPILE_STATUS 0x8B81
#define GL_LINK_STATUS 0x8B82
#define WGL_CONTEXT_MAJOR_VERSION_ARB 0x2091
#define WGL_CONTEXT_MINOR_VERSION_ARB 0x2092
#define WGL_CONTEXT_PROFILE_MASK_ARB 0x9126
#define WGL_CONTEXT_CORE_PROFILE_BIT_ARB 0x1

#define FN(ret, name, args) typedef ret(WINAPI* PFN_##name) args; PFN_##name p##name;
FN(GLuint, glCreateShader, (GLenum))
FN(void, glShaderSource, (GLuint, GLsizei, const GLchar* const*, const GLint*))
FN(void, glCompileShader, (GLuint))
FN(void, glGetShaderiv, (GLuint, GLenum, GLint*))
FN(void, glGetShaderInfoLog, (GLuint, GLsizei, GLsizei*, GLchar*))
FN(GLuint, glCreateProgram, (void))
FN(void, glAttachShader, (GLuint, GLuint))
FN(void, glLinkProgram, (GLuint))
FN(void, glGetProgramiv, (GLuint, GLenum, GLint*))
FN(void, glGetProgramInfoLog, (GLuint, GLsizei, GLsizei*, GLchar*))
typedef HGLRC(WINAPI* PFN_wglCreateContextAttribsARB)(HDC, HGLRC, const int*);

static std::string Read(const char* path)
{
    std::ifstream f(path, std::ios::binary);
    std::stringstream s;
    s << f.rdbuf();
    return s.str();
}

static double Ms(std::chrono::steady_clock::time_point a)
{
    return std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - a).count();
}

static GLuint Compile(GLenum type, const std::string& src, const char* name)
{
    GLuint s = pglCreateShader(type);
    const GLchar* p = src.c_str();
    pglShaderSource(s, 1, &p, nullptr);
    auto t = std::chrono::steady_clock::now();
    pglCompileShader(s);
    GLint ok = 0;
    pglGetShaderiv(s, GL_COMPILE_STATUS, &ok);
    printf("compile %s: %.0f ms ok=%d\n", name, Ms(t), ok);
    if (!ok) {
        char log[4096] = {};
        pglGetShaderInfoLog(s, sizeof(log) - 1, nullptr, log);
        printf("%s\n", log);
    }
    return s;
}

int main(int argc, char** argv)
{
    if (argc < 3) {
        printf("gstime <vs> <fs> [gs]\n");
        return 1;
    }
    HWND w = CreateWindowA("STATIC", "gstime", WS_POPUP, 0, 0, 64, 64, nullptr, nullptr, nullptr, nullptr);
    HDC dc = GetDC(w);
    PIXELFORMATDESCRIPTOR pfd{sizeof(pfd), 1, PFD_DRAW_TO_WINDOW | PFD_SUPPORT_OPENGL | PFD_DOUBLEBUFFER, PFD_TYPE_RGBA, 32};
    SetPixelFormat(dc, ChoosePixelFormat(dc, &pfd), &pfd);
    HGLRC legacy = wglCreateContext(dc);
    wglMakeCurrent(dc, legacy);
    auto create = (PFN_wglCreateContextAttribsARB)wglGetProcAddress("wglCreateContextAttribsARB");
    const int attrs[] = {WGL_CONTEXT_MAJOR_VERSION_ARB, 4, WGL_CONTEXT_MINOR_VERSION_ARB, 4, WGL_CONTEXT_PROFILE_MASK_ARB,
                         WGL_CONTEXT_CORE_PROFILE_BIT_ARB, 0};
    HGLRC rc = create(dc, nullptr, attrs);
    wglMakeCurrent(dc, rc);
#define LOAD(name) p##name = (PFN_##name)wglGetProcAddress(#name);
    LOAD(glCreateShader) LOAD(glShaderSource) LOAD(glCompileShader) LOAD(glGetShaderiv) LOAD(glGetShaderInfoLog)
    LOAD(glCreateProgram) LOAD(glAttachShader) LOAD(glLinkProgram) LOAD(glGetProgramiv) LOAD(glGetProgramInfoLog)
    printf("%s | %s\n", (const char*)glGetString(GL_RENDERER), (const char*)glGetString(GL_VERSION));
    char tag[64];
    sprintf_s(tag, "\n// %llu\n", (unsigned long long)GetTickCount64() ^ ((unsigned long long)GetCurrentProcessId() << 32));
    auto t0 = std::chrono::steady_clock::now();
    GLuint prog = pglCreateProgram();
    pglAttachShader(prog, Compile(GL_VERTEX_SHADER, Read(argv[1]) + tag, "vs"));
    pglAttachShader(prog, Compile(GL_FRAGMENT_SHADER, Read(argv[2]) + tag, "fs"));
    if (argc > 3) pglAttachShader(prog, Compile(GL_GEOMETRY_SHADER, Read(argv[3]) + tag, "gs"));
    auto t = std::chrono::steady_clock::now();
    pglLinkProgram(prog);
    GLint ok = 0;
    pglGetProgramiv(prog, GL_LINK_STATUS, &ok);
    printf("link: %.0f ms ok=%d | total %.0f ms\n", Ms(t), ok, Ms(t0));
    if (!ok) {
        char log[4096] = {};
        pglGetProgramInfoLog(prog, sizeof(log) - 1, nullptr, log);
        printf("%s\n", log);
    }
    return 0;
}
