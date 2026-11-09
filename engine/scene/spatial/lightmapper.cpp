/**************************************************************************/
/*  lightmapper.cpp                                                       */

#include "lightmapper.h"

Ref<LightmapDenoiser> (*LightmapDenoiser::create_function)() = nullptr;

Ref<LightmapDenoiser> LightmapDenoiser::create() {
	if (create_function) {
		return Ref<LightmapDenoiser>(create_function());
	}
	return Ref<LightmapDenoiser>();
}

Ref<LightmapRaycaster> (*LightmapRaycaster::create_function)() = nullptr;

Ref<LightmapRaycaster> LightmapRaycaster::create() {
	if (create_function) {
		return Ref<LightmapRaycaster>(create_function());
	}
	return Ref<LightmapRaycaster>();
}

Lightmapper::CreateFunc Lightmapper::create_custom = nullptr;
Lightmapper::CreateFunc Lightmapper::create_gpu = nullptr;
Lightmapper::CreateFunc Lightmapper::create_cpu = nullptr;

Ref<Lightmapper> Lightmapper::create() {
	Ref<Lightmapper> lm;
	if (create_custom) {
		lm = create_custom();
	}

	if (lm.is_null() && create_gpu) {
		lm = create_gpu();
	}

	if (lm.is_null() && create_cpu) {
		lm = create_cpu();
	}
	if (lm.is_null()) {
		return Ref<Lightmapper>();
	} else {
		return Ref<Lightmapper>(lm);
	}
}

Lightmapper::Lightmapper() {
}
