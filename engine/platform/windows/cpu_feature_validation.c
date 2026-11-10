/**************************************************************************/
/*  cpu_feature_validation.c                                              */

#include <windows.h>
#ifdef _MSC_VER
#include <intrin.h> // For builtin __cpuid.
#else
void __cpuid(int *r_cpuinfo, int p_info) {
	// Note: Some compilers have a buggy `__cpuid` intrinsic, using inline assembly (based on LLVM-20 implementation) instead.
	__asm__ __volatile__(
			"xchgq %%rbx, %q1;"
			"cpuid;"
			"xchgq %%rbx, %q1;"
			: "=a"(r_cpuinfo[0]), "=r"(r_cpuinfo[1]), "=c"(r_cpuinfo[2]), "=d"(r_cpuinfo[3])
			: "0"(p_info));
}
#endif

#ifndef PF_SSE4_2_INSTRUCTIONS_AVAILABLE
#define PF_SSE4_2_INSTRUCTIONS_AVAILABLE 38
#endif

#ifdef WINDOWS_SUBSYSTEM_CONSOLE
extern int WINAPI mainCRTStartup();
#else
extern int WINAPI WinMainCRTStartup();
#endif

#if defined(__GNUC__) || defined(__clang__)
extern int WINAPI ShimMainCRTStartup() __attribute__((used));
#endif

extern int WINAPI ShimMainCRTStartup() {
	BOOL win_sse42_supported = FALSE;
	BOOL cpuid_sse42_supported = FALSE;

	int cpuinfo[4];
	__cpuid(cpuinfo, 0x01);

	win_sse42_supported = IsProcessorFeaturePresent(PF_SSE4_2_INSTRUCTIONS_AVAILABLE);
	cpuid_sse42_supported = cpuinfo[2] & (1 << 20);

	if (win_sse42_supported || cpuid_sse42_supported) {
#ifdef WINDOWS_SUBSYSTEM_CONSOLE
		return mainCRTStartup();
#else
		return WinMainCRTStartup();
#endif
	} else {
		MessageBoxW(NULL, L"A CPU with SSE4.2 instruction set support is required.", L"Orblit Engine", MB_OK | MB_ICONEXCLAMATION | MB_TASKMODAL);
		return -1;
	}
}
