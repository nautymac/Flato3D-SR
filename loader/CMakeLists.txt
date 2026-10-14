cmake_minimum_required(VERSION 3.20)
# Flato3D loader: version.dll proxy that redirects system graphics DLL loads to the wrappers in the
# game folder. Builds for x64 (-A x64) and x86 (-A Win32); see build.ps1.
project(Flato3DLoader LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_MSVC_RUNTIME_LIBRARY "MultiThreaded")
if(CMAKE_SIZEOF_VOID_P EQUAL 8)
    set(ARCH x64)
else()
    set(ARCH x86)
endif()
add_library(version SHARED loader.cpp version.def)
target_compile_options(version PRIVATE /W3 /utf-8)
target_compile_definitions(version PRIVATE NOMINMAX WIN32_LEAN_AND_MEAN _CRT_SECURE_NO_WARNINGS)
set_target_properties(version PROPERTIES
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${CMAKE_SOURCE_DIR}/bin/${ARCH}"
    LIBRARY_OUTPUT_DIRECTORY_RELEASE "${CMAKE_SOURCE_DIR}/bin/${ARCH}")
