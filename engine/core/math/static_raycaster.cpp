/**************************************************************************/
/*  static_raycaster.cpp                                                  */

#include "static_raycaster.h"

Ref<StaticRaycaster> (*StaticRaycaster::create_function)() = nullptr;

Ref<StaticRaycaster> StaticRaycaster::create() {
	if (create_function) {
		return Ref<StaticRaycaster>(create_function());
	}
	return Ref<StaticRaycaster>();
}
