/**************************************************************************/
/*  box_container.h                                                       */

#pragma once

#include "scene/ui/container.h"

class BoxContainer : public Container {
	ORBLIT_CLASS(BoxContainer, Container);

public:
	enum AlignmentMode {
		ALIGNMENT_BEGIN,
		ALIGNMENT_CENTER,
		ALIGNMENT_END
	};

private:
	bool vertical = false;
	AlignmentMode alignment = ALIGNMENT_BEGIN;

	struct ThemeCache {
		int separation = 0;
	} theme_cache;

	bool reverse_sort = false;

	void _resort();
	Size2 _get_minimum_size(bool p_use_desired_sizes) const;

protected:
	bool is_fixed = false;

	void _notification(int p_what);
	void _validate_property(PropertyInfo &p_property) const;
	static void register_members();

public:
	Control *add_spacer(bool p_begin = false);

	void set_alignment(AlignmentMode p_alignment);
	AlignmentMode get_alignment() const;

	void set_vertical(bool p_vertical);
	bool is_vertical() const;

	void set_reverse_sort(bool p_reverse_sort);
	bool is_reverse_sort() const;

	virtual Size2 get_minimum_size() const override;
	virtual Size2 get_desired_size() const override;

	virtual Vector<int> get_allowed_size_flags_horizontal() const override;
	virtual Vector<int> get_allowed_size_flags_vertical() const override;

	BoxContainer(bool p_vertical = false);
};

class HBoxContainer : public BoxContainer {
	ORBLIT_CLASS(HBoxContainer, BoxContainer);

public:
	HBoxContainer() :
			BoxContainer(false) { is_fixed = true; }
};

class MarginContainer;
class VBoxContainer : public BoxContainer {
	ORBLIT_CLASS(VBoxContainer, BoxContainer);

public:
	MarginContainer *add_margin_child(const String &p_label, Control *p_control, bool p_expand = false);

	VBoxContainer() :
			BoxContainer(true) { is_fixed = true; }
};

VARIANT_ENUM_CAST(BoxContainer::AlignmentMode);
