/**************************************************************************/
/*  editor_resource_conversion_plugin.h                                   */

#pragma once

#include "core/io/resource.h"
#include "core/object/orblit_virtual.gen.h"

class EditorResourceConversionPlugin : public RefCounted {
	ORBLIT_CLASS(EditorResourceConversionPlugin, RefCounted);

protected:
	static void register_members();

	ORBLIT_VIRTUAL0RC(String, _converts_to)
	ORBLIT_VIRTUAL1RC(bool, _handles, Ref<Resource>)
	ORBLIT_VIRTUAL1RC(Ref<Resource>, _convert, Ref<Resource>)

public:
	virtual String converts_to() const;
	virtual bool handles(const Ref<Resource> &p_resource) const;
	virtual Ref<Resource> convert(const Ref<Resource> &p_resource) const;
};
