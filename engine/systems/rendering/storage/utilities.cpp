/**************************************************************************/
/*  utilities.cpp                                                         */

#include "utilities.h"

void Dependency::changed_notify(DependencyChangedNotification p_notification) {
	for (const KeyValue<DependencyTracker *, uint32_t> &E : instances) {
		if (E.key->changed_callback) {
			E.key->changed_callback(p_notification, E.key);
		}
	}
}

void Dependency::deleted_notify(const RID &p_rid) {
	for (const KeyValue<DependencyTracker *, uint32_t> &E : instances) {
		if (E.key->deleted_callback) {
			E.key->deleted_callback(p_rid, E.key);
		}
	}
	for (const KeyValue<DependencyTracker *, uint32_t> &E : instances) {
		E.key->dependencies.erase(this);
	}
	instances.clear();
}

Dependency::~Dependency() {
#ifdef DEBUG_ENABLED
	if (instances.size()) {
		WARN_PRINT("Leaked instance dependency: Bug - did not call instance_notify_deleted when freeing.");
		for (const KeyValue<DependencyTracker *, uint32_t> &E : instances) {
			E.key->dependencies.erase(this);
		}
	}
#endif
}
