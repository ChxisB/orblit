/**************************************************************************/
/*  editor_properties_vector.h                                            */

#pragma once

#include "editor/inspector/editor_inspector.h"
#include "editor/inspector/editor_properties.h"

class EditorSpinSlider;
class TextureButton;

class EditorPropertyVectorN : public EditorProperty {
	ORBLIT_CLASS(EditorPropertyVectorN, EditorProperty);

	static const String COMPONENT_LABELS[4];

	int component_count = 0;
	Variant::Type vector_type;

	Vector<EditorSpinSlider *> spin_sliders;
	TextureButton *linked = nullptr;
	Vector<double> ratio;

	bool radians_as_degrees = false;

	void _update_ratio();
	void _store_link(bool p_linked);
	void _value_changed(double p_val, const String &p_name);

protected:
	virtual void _set_read_only(bool p_read_only) override;
	void _notification(int p_what);

public:
	virtual void set_deferred_drag_mode_enabled(bool p_enabled = true) override;
	virtual void update_property() override;
	void setup(const EditorPropertyRangeHint &p_range_hint, bool p_link = false, bool p_is_int = false);
	EditorPropertyVectorN(Variant::Type p_type, bool p_force_wide, bool p_horizontal);
};

class EditorPropertyVector2 : public EditorPropertyVectorN {
	ORBLIT_CLASS(EditorPropertyVector2, EditorPropertyVectorN);

public:
	EditorPropertyVector2(bool p_force_wide = false);
};

class EditorPropertyVector2i : public EditorPropertyVectorN {
	ORBLIT_CLASS(EditorPropertyVector2i, EditorPropertyVectorN);

public:
	EditorPropertyVector2i(bool p_force_wide = false);
};

class EditorPropertyVector3 : public EditorPropertyVectorN {
	ORBLIT_CLASS(EditorPropertyVector3, EditorPropertyVectorN);

public:
	EditorPropertyVector3(bool p_force_wide = false);
};

class EditorPropertyVector3i : public EditorPropertyVectorN {
	ORBLIT_CLASS(EditorPropertyVector3i, EditorPropertyVectorN);

public:
	EditorPropertyVector3i(bool p_force_wide = false);
};

class EditorPropertyVector4 : public EditorPropertyVectorN {
	ORBLIT_CLASS(EditorPropertyVector4, EditorPropertyVectorN);

public:
	EditorPropertyVector4(bool p_force_wide = false);
};

class EditorPropertyVector4i : public EditorPropertyVectorN {
	ORBLIT_CLASS(EditorPropertyVector4i, EditorPropertyVectorN);

public:
	EditorPropertyVector4i(bool p_force_wide = false);
};
