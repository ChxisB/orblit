/**************************************************************************/
/*  flow_container.h                                                      */

#pragma once

#include "scene/ui/container.h"

class FlowContainer : public Container {
	ORBLIT_CLASS(FlowContainer, Container);

public:
	enum AlignmentMode {
		ALIGNMENT_BEGIN,
		ALIGNMENT_CENTER,
		ALIGNMENT_END
	};
	enum LastWrapAlignmentMode {
		LAST_WRAP_ALIGNMENT_INHERIT,
		LAST_WRAP_ALIGNMENT_BEGIN,
		LAST_WRAP_ALIGNMENT_CENTER,
		LAST_WRAP_ALIGNMENT_END
	};

private:
	int cached_size = 0;
	int cached_line_count = 0;
	int cached_line_max_child_count = 0;

	bool vertical = false;
	bool reverse_fill = false;
	AlignmentMode alignment = ALIGNMENT_BEGIN;
	LastWrapAlignmentMode last_wrap_alignment = LAST_WRAP_ALIGNMENT_INHERIT;

	struct ThemeCache {
		int h_separation = 0;
		int v_separation = 0;
	} theme_cache;

	void _resort();
	Size2 _get_minimum_size(bool p_use_desired_sizes) const;

protected:
	bool is_fixed = false;

	void _notification(int p_what);
	void _validate_property(PropertyInfo &p_property) const;
	static void register_members();

public:
	int get_line_count() const;
	int get_line_max_child_count() const;

	void set_alignment(AlignmentMode p_alignment);
	AlignmentMode get_alignment() const;

	void set_last_wrap_alignment(LastWrapAlignmentMode p_last_wrap_alignment);
	LastWrapAlignmentMode get_last_wrap_alignment() const;

	void set_vertical(bool p_vertical);
	bool is_vertical() const;

	void set_reverse_fill(bool p_reverse_fill);
	bool is_reverse_fill() const;

	virtual Size2 get_minimum_size() const override;
	virtual Size2 get_desired_size() const override;

	virtual Vector<int> get_allowed_size_flags_horizontal() const override;
	virtual Vector<int> get_allowed_size_flags_vertical() const override;

	FlowContainer(bool p_vertical = false);
};

class HFlowContainer : public FlowContainer {
	ORBLIT_CLASS(HFlowContainer, FlowContainer);

public:
	HFlowContainer() :
			FlowContainer(false) { is_fixed = true; }
};

class VFlowContainer : public FlowContainer {
	ORBLIT_CLASS(VFlowContainer, FlowContainer);

public:
	VFlowContainer() :
			FlowContainer(true) { is_fixed = true; }
};

VARIANT_ENUM_CAST(FlowContainer::AlignmentMode);
VARIANT_ENUM_CAST(FlowContainer::LastWrapAlignmentMode);
