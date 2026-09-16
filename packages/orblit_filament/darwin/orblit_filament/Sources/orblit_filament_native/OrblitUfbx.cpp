// ufbx, compiled here and nowhere else.
//
// Vendored from https://github.com/ufbx/ufbx at tag v0.23.0, commit
// fcc5d6ba444cfd3eb80677dba5e37e493941abe5, as third_party/ufbx/ufbx.h and
// ufbx.c, unmodified. MIT or public domain, at the user's choice; the licence
// is LICENSES/ufbx.txt at the repository root. To update: fetch both files
// from a newer tag, change the tag and commit above, and run
// native/headless/build.sh test, whose import check reads real files.
//
// Why a .cpp that includes a .c. Every build of the renderer core takes the
// top-level .cpp files beside this one — the headless, host and web scripts
// by glob, the Linux, Windows and Android CMake files by list — and none of
// them compiles C: the CMake projects declare LANGUAGES CXX only, and the
// scripts call clang++ and em++. ufbx says it compiles as C++ and is tested
// that way, so compiling it through this one file is a line in each source
// list rather than a C toolchain added to five builds. Swift Package Manager
// compiles every file under the target, so Package.swift excludes ufbx.c,
// which would otherwise be linked a second time; CocoaPods' pattern does not
// match .c at all.
//
// Nothing else includes ufbx.c. OrblitImport.cpp includes only the header.

// No stdio. The importer hands ufbx the file in memory and answers every
// request for a file beside it (an OBJ's .mtl) itself, through the renderer's
// resources, so there is nothing ufbx should ever open by path. With this
// defined it cannot: a reference in an untrusted file to /etc/passwd, or to
// a geometry cache somewhere on disk, has no route to the file system even if
// a callback were missed. It also keeps fopen out of a browser build.
#define UFBX_NO_STDIO

// ufbx pushes and pops its own warning suppressions for the warnings it
// knows its code raises. These are for the rest, under whatever a given build
// turns on (-Wextra here, /permissive- on Windows), so that a warning in
// thirty thousand lines of vendored code is never mistaken for one in ours.
// Local to this file; no build's global flags change. Clang and MSVC only,
// because those are the compilers every build uses; ufbx quiets GCC itself.
#if defined(__clang__)
#pragma clang diagnostic push
#pragma clang diagnostic ignored "-Wall"
#pragma clang diagnostic ignored "-Wextra"
#elif defined(_MSC_VER)
#pragma warning(push, 0)
#endif

#include "third_party/ufbx/ufbx.c"

#if defined(__clang__)
#pragma clang diagnostic pop
#elif defined(_MSC_VER)
#pragma warning(pop)
#endif
