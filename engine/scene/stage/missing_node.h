/**************************************************************************/
/*  missing_node.h                                                        */

#pragma once

#include "scene/stage/entity.h"

class MissingNode : public Entity {
	ORBLIT_CLASS(MissingNode, Entity)
	HashMap<StringName, Variant> properties;

	String original_class;
	String original_scene;
	bool recording_properties = false;
	bool recording_signals = false;

protected:
	bool _set(const StringName &p_name, const Variant &p_value);
	bool _get(const StringName &p_name, Variant &r_ret) const;
	void _get_property_list(List<PropertyInfo> *p_list) const;

#ifdef DEBUG_ENABLED
	virtual Error connect(const StringName &p_signal, const Callable &p_callable, uint32_t p_flags = 0) override;
#endif

	static void register_members();

public:
	void set_original_class(const String &p_class);
	String get_original_class() const;

	void set_original_scene(const String &p_scene);
	String get_original_scene() const;

	void set_recording_properties(bool p_enable);
	bool is_recording_properties() const;

	void set_recording_signals(bool p_enable);
	bool is_recording_signals() const;

	virtual PackedStringArray get_configuration_warnings() const override;

	MissingNode();
};
