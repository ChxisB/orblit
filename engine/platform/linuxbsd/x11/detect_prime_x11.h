/**************************************************************************/
/*  detect_prime_x11.h                                                    */

#pragma once

#if defined(X11_ENABLED) && defined(GLES3_ENABLED)

class DetectPrimeX11 {
private:
	struct Vendor {
		const char *glxvendor = nullptr;
		int priority = 0;
	};

	static constexpr Vendor vendor_map[] = {
		{ "Advanced Micro Devices, Inc.", 30 },
		{ "AMD", 30 },
		{ "NVIDIA Corporation", 30 },
		{ "X.Org", 30 },
		{ "Intel Open Source Technology Center", 20 },
		{ "Intel", 20 },
		{ "nouveau", 10 },
		{ "Mesa Project", 0 },
		{ nullptr, 0 }
	};

public:
	static void create_context();

	static int detect_prime();
};

#endif // X11_ENABLED && GLES3_ENABLED
