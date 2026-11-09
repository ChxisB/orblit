/**************************************************************************/
/*  bit_map_editor_plugin.cpp                                             */

#include "bit_map_editor_plugin.h"

#include "core/object/callable_mp.h"
#include "editor/editor_string_names.h"
#include "editor/themes/editor_scale.h"
#include "scene/resources/image_texture.h"
#include "scene/ui/aspect_ratio_container.h"
#include "scene/ui/label.h"
#include "scene/ui/margin_container.h"
#include "scene/ui/texture_rect.h"

void BitMapEditor::setup(const Ref<BitMap> &p_bitmap) {
	Ref<ImageTexture> bitmap_texture = ImageTexture::create_from_image(p_bitmap->convert_to_image());
	texture_rect->set_texture(bitmap_texture);
	if (bitmap_texture.is_valid()) {
		centering_container->set_custom_minimum_size(Size2(0, 250) * EDSCALE);
		centering_container->set_ratio(bitmap_texture->get_size().aspect());
		outline_overlay->connect(SceneStringName(draw), callable_mp(this, &BitMapEditor::_draw_outline));
	}
	size_label->set_text(vformat(U"%s×%s", p_bitmap->get_size().width, p_bitmap->get_size().height));
}

void BitMapEditor::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_THEME_CHANGED: {
			cached_outline_color = get_theme_color(SNAME("extra_border_color_1"), EditorStringName(Editor));
		} break;
	}
}

void BitMapEditor::_draw_outline() {
	const float outline_width = Math::round(EDSCALE);
	const Rect2 outline_rect = Rect2(Vector2(), texture_rect->get_size()).grow(outline_width * 0.5);
	outline_overlay->draw_rect(outline_rect, cached_outline_color, false, outline_width);
}

BitMapEditor::BitMapEditor() {
	MarginContainer *margin_container = memnew(MarginContainer);
	const float outline_width = Math::round(EDSCALE);
	margin_container->add_theme_constant_override("margin_right", outline_width);
	margin_container->add_theme_constant_override("margin_top", outline_width);
	margin_container->add_theme_constant_override("margin_left", outline_width);
	margin_container->add_theme_constant_override("margin_bottom", outline_width);
	add_child(margin_container);

	centering_container = memnew(AspectRatioContainer);
	margin_container->add_child(centering_container);

	texture_rect = memnew(TextureRect);
	texture_rect->set_texture_filter(TEXTURE_FILTER_NEAREST);
	texture_rect->set_expand_mode(TextureRect::EXPAND_IGNORE_SIZE);
	centering_container->add_child(texture_rect);

	outline_overlay = memnew(Control);
	centering_container->add_child(outline_overlay);

	size_label = memnew(Label);
	size_label->set_focus_mode(FOCUS_ACCESSIBILITY);
	size_label->set_horizontal_alignment(HORIZONTAL_ALIGNMENT_RIGHT);
	add_child(size_label);

	// Reduce extra padding on top and bottom of size label.
	Ref<StyleBoxEmpty> stylebox;
	stylebox.instantiate();
	stylebox->set_content_margin(SIDE_RIGHT, 4 * EDSCALE);
	size_label->add_theme_style_override(CoreStringName(normal), stylebox);
}

///////////////////////

bool EditorInspectorPluginBitMap::can_handle(Object *p_object) {
	return Object::cast_to<BitMap>(p_object) != nullptr;
}

void EditorInspectorPluginBitMap::parse_begin(Object *p_object) {
	BitMap *bitmap = Object::cast_to<BitMap>(p_object);
	if (!bitmap) {
		return;
	}
	Ref<BitMap> bm(bitmap);

	BitMapEditor *editor = memnew(BitMapEditor);
	editor->setup(bm);
	add_custom_control(editor);
}

///////////////////////

BitMapEditorPlugin::BitMapEditorPlugin() {
	Ref<EditorInspectorPluginBitMap> plugin;
	plugin.instantiate();
	add_inspector_plugin(plugin);
}
