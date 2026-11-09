/**************************************************************************/
/*  aspect_ratio_container.h                                              */

#pragma once

#include "scene/ui/container.h"

class AspectRatioContainer : public Container {
	ORBLIT_CLASS(AspectRatioContainer, Container);

protected:
	void _notification(int p_what);
	static void register_members();
	virtual Size2 get_minimum_size() const override;

public:
	enum StretchMode {
		STRETCH_WIDTH_CONTROLS_HEIGHT,
		STRETCH_HEIGHT_CONTROLS_WIDTH,
		STRETCH_FIT,
		STRETCH_COVER,
	};
	enum AlignmentMode {
		ALIGNMENT_BEGIN,
		ALIGNMENT_CENTER,
		ALIGNMENT_END,
	};

private:
	float ratio = 1.0;
	StretchMode stretch_mode = STRETCH_FIT;
	AlignmentMode alignment_horizontal = ALIGNMENT_CENTER;
	AlignmentMode alignment_vertical = ALIGNMENT_CENTER;

public:
	void set_ratio(float p_ratio);
	float get_ratio() const { return ratio; }

	void set_stretch_mode(StretchMode p_mode);
	StretchMode get_stretch_mode() const { return stretch_mode; }

	void set_alignment_horizontal(AlignmentMode p_alignment_horizontal);
	AlignmentMode get_alignment_horizontal() const { return alignment_horizontal; }

	void set_alignment_vertical(AlignmentMode p_alignment_vertical);
	AlignmentMode get_alignment_vertical() const { return alignment_vertical; }

	virtual Vector<int> get_allowed_size_flags_horizontal() const override;
	virtual Vector<int> get_allowed_size_flags_vertical() const override;
};

VARIANT_ENUM_CAST(AspectRatioContainer::StretchMode);
VARIANT_ENUM_CAST(AspectRatioContainer::AlignmentMode);
