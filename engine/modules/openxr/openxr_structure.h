/**************************************************************************/
/*  openxr_structure.h                                                    */

#pragma once

#include "core/object/orblit_virtual.gen.h"
#include "core/object/ref_counted.h"

#include <openxr/openxr.h>

// Base class for XrStructureType based headers
class OpenXRStructureBase : public RefCounted {
	ORBLIT_CLASS(OpenXRStructureBase, RefCounted);

public:
	/*
	 * get_header should return a pointer to a proper XrStructureType structure.
	 * The pointer should remain valid as long as this object is not destructed.
	 *
	 * This function should be implemented based on the following template:
	 * void *get_header(void *p_next = nullptr) {
	 *     my_xr_struct.type = XR_TYPE_XYZ;
	 *     if (get_next().is_valid()) {
	 *         my_xr_struct.next = get_next()->get_header(p_next);
	 *     } else {
	 *         my_xr_struct.next = p_next
	 *     }
	 *
	 *     // add further setup of the struct here
	 *
	 *     return &my_xr_struct;
	 * }
	 */
	virtual void *get_header(void *p_next = nullptr);
	virtual XrStructureType get_structure_type();

	void set_next(const Ref<OpenXRStructureBase> p_next);
	Ref<OpenXRStructureBase> get_next() const;

	ORBLIT_VIRTUAL1R(uint64_t, _get_header, uint64_t);

protected:
	static void register_members();

private:
	Ref<OpenXRStructureBase> next;

	uint64_t _get_headergd(uint64_t p_next);
	uint64_t _get_structure_type();
};
