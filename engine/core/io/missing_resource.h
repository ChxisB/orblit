/**************************************************************************/
/*  missing_resource.h                                                    */

#pragma once

#include "core/io/resource.h"

#define META_PROPERTY_MISSING_RESOURCES "metadata/_missing_resources"
#define META_MISSING_RESOURCES "_missing_resources"

class MissingResource : public Resource {
	ORBLIT_CLASS(MissingResource, Resource)
	HashMap<StringName, Variant> properties;

	String original_class;
	bool recording_properties = false;

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

	static void register_members();

public:
	void set_original_class(const String &p_class);
	String get_original_class() const;

	void set_recording_properties(bool p_enable);
	bool is_recording_properties() const;

	virtual String get_save_class() const override;
};
