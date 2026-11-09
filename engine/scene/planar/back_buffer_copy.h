/**************************************************************************/
/*  back_buffer_copy.h                                                    */

#pragma once

#include "scene/planar/entity_planar.h"

class BackBufferCopy : public Entity2D {
	ORBLIT_CLASS(BackBufferCopy, Entity2D);

public:
	enum CopyMode {
		COPY_MODE_DISABLED,
		COPY_MODE_RECT,
		COPY_MODE_VIEWPORT
	};

private:
	Rect2 rect = Rect2(-100, -100, 200, 200);
	CopyMode copy_mode = COPY_MODE_RECT;

	void _update_copy_mode();

protected:
	static void register_members();
	void _validate_property(PropertyInfo &p_property) const;

public:
#ifdef DEBUG_ENABLED
	Rect2 _edit_get_rect() const override;
	virtual bool _edit_use_rect() const override;
#endif // DEBUG_ENABLED

	void set_rect(const Rect2 &p_rect);
	Rect2 get_rect() const;
	Rect2 get_anchorable_rect() const override;

	void set_copy_mode(CopyMode p_mode);
	CopyMode get_copy_mode() const;

	BackBufferCopy();
	~BackBufferCopy();
};

VARIANT_ENUM_CAST(BackBufferCopy::CopyMode);
