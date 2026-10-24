cmake_minimum_required(VERSION 3.20)
# Flato3D loader shim: <name>.dll that forwards everything to the system <name>.dll and imports the loader
# (version.dll), for games without a version.dll import. SHIM_NAME picks the system DLL (default winmm).
# Builds for x64 (-A x64) and x86 (-A Win32); see ..\build.ps1.
project(Flato3DLoaderShim LANGUAGES CXX)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded")
if(NOT SHIM_NAME)
    set(SHIM_NAME winmm)
endif()
find_package(Python3 REQUIRED COMPONENTS Interpreter)
if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    set(ARCH x64)
    set(SYS_DLL "$ENV{SystemRoot}/System32/${SHIM_NAME}.dll")
else()
    set(ARCH x86)
    set(SYS_DLL "$ENV{SystemRoot}/SysWOW64/${SHIM_NAME}.dll")
endif()
set(GEN "${CMAKE_BINARY_DIR}/gen")
add_custom_command(
    OUTPUT "${GEN}/forwarders.h"
    COMMAND ${CMAKE_COMMAND} -E make_directory "${GEN}"
    COMMAND Python3::Interpreter -I "${CMAKE_SOURCE_DIR}/gen_forwarders.py" "${SYS_DLL}" ${ARCH} "${GEN}/forwarders.h"
    DEPENDS "${CMAKE_SOURCE_DIR}/gen_forwarders.py"
    VERBATIM)
add_library(shim SHARED shim.cpp "${GEN}/forwarders.h")
target_include_directories(shim PRIVATE "${GEN}")
target_link_libraries(shim PRIVATE version)
target_compile_options(shim PRIVATE /W3 /utf-8)
target_link_options(shim PRIVATE /NODEFAULTLIB:msvcrt.lib)
set_target_properties(shim PROPERTIES
    OUTPUT_NAME ${SHIM_NAME}
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_SOURCE_DIR}/../bin/${ARCH}/shim"
    LIBRARY_OUTPUT_DIRECTORY_RELEASE "${CMAKE_SOURCE_DIR}/../bin/${ARCH}/shim")
