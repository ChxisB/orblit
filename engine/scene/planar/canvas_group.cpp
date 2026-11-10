/**************************************************************************/
/*  canvas_group.cpp                                                      */

#include "canvas_group.h"

#include "core/object/class_registry.h"
#include "systems/rendering/rendering_system.h"

void CanvasGroup::set_fit_margin(real_t p_fit_margin) {
	ERR_FAIL_COND(p_fit_margin < 0.0);

	fit_margin = p_fit_margin;
	RS::get_singleton()->canvas_item_set_canvas_group_mode(get_canvas_item(), RSE::CANVAS_GROUP_MODE_TRANSPARENT, clear_margin, true, fit_margin, use_mipmaps);

	queue_redraw();
}

real_t CanvasGroup::get_fit_margin() const {
	return fit_margin;
}

void CanvasGroup::set_clear_margin(real_t p_clear_margin) {
	ERR_FAIL_COND(p_clear_margin < 0.0);

	clear_margin = p_clear_margin;
	RS::get_singleton()->canvas_item_set_canvas_group_mode(get_canvas_item(), RSE::CANVAS_GROUP_MODE_TRANSPARENT, clear_margin, true, fit_margin, use_mipmaps);

	queue_redraw();
}

real_t CanvasGroup::get_clear_margin() const {
	return clear_margin;
}

void CanvasGroup::set_use_mipmaps(bool p_use_mipmaps) {
	use_mipmaps = p_use_mipmaps;
	RS::get_singleton()->canvas_item_set_canvas_group_mode(get_canvas_item(), RSE::CANVAS_GROUP_MODE_TRANSPARENT, clear_margin, true, fit_margin, use_mipmaps);
}
bool CanvasGroup::is_using_mipmaps() const {
	return use_mipmaps;
}

PackedStringArray CanvasGroup::get_configuration_warnings() const {
	PackedStringArray warnings = Entity2D::get_configuration_warnings();

	if (is_inside_stage()) {
		bool warned_about_ancestor_clipping = false;
		bool warned_about_canvasgroup_ancestor = false;
		Entity *n = get_parent();
		while (n) {
			CanvasItem *as_canvas_item = Object::cast_to<CanvasItem>(n);
			if (!warned_about_ancestor_clipping && as_canvas_item && as_canvas_item->get_clip_children_mode() != CLIP_CHILDREN_DISABLED) {
				warnings.push_back(vformat(RTR("Ancestor \"%s\" clips its children, so this CanvasGroup will not function properly."), as_canvas_item->get_name()));
				warned_about_ancestor_clipping = true;
			}

			CanvasGroup *as_canvas_group = Object::cast_to<CanvasGroup>(n);
			if (!warned_about_canvasgroup_ancestor && as_canvas_group) {
				warnings.push_back(vformat(RTR("Ancestor \"%s\" is a CanvasGroup, so this CanvasGroup will not function properly."), as_canvas_group->get_name()));
				warned_about_canvasgroup_ancestor = true;
			}

			// Only break out early once both warnings have been triggered, so
			// that the user is aware of both possible reasons for clipping not working.
			if (warned_about_ancestor_clipping && warned_about_canvasgroup_ancestor) {
				break;
			}
			n = n->get_parent();
		}
	}

	return warnings;
}

void CanvasGroup::register_members() {
	ClassRegistry::bind_method(D_METHOD("set_fit_margin", "fit_margin"), &CanvasGroup::set_fit_margin);
	ClassRegistry::bind_method(D_METHOD("get_fit_margin"), &CanvasGroup::get_fit_margin);

	ClassRegistry::bind_method(D_METHOD("set_clear_margin", "clear_margin"), &CanvasGroup::set_clear_margin);
	ClassRegistry::bind_method(D_METHOD("get_clear_margin"), &CanvasGroup::get_clear_margin);

	ClassRegistry::bind_method(D_METHOD("set_use_mipmaps", "use_mipmaps"), &CanvasGroup::set_use_mipmaps);
	ClassRegistry::bind_method(D_METHOD("is_using_mipmaps"), &CanvasGroup::is_using_mipmaps);

	ADD_GROUP("Tweaks", "");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "fit_margin", PROPERTY_HINT_RANGE, "0,1024,1.0,or_greater,suffix:px"), "set_fit_margin", "get_fit_margin");
	ADD_PROPERTY(PropertyInfo(Variant::FLOAT, "clear_margin", PROPERTY_HINT_RANGE, "0,1024,1.0,or_greater,suffix:px"), "set_clear_margin", "get_clear_margin");
	ADD_PROPERTY(PropertyInfo(Variant::BOOL, "use_mipmaps"), "set_use_mipmaps", "is_using_mipmaps");
}

CanvasGroup::CanvasGroup() {
	set_fit_margin(10.0); //sets things
}
CanvasGroup::~CanvasGroup() {
	RS::get_singleton()->canvas_item_set_canvas_group_mode(get_canvas_item(), RSE::CANVAS_GROUP_MODE_DISABLED);
}
