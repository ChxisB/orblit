/**************************************************************************/
/*  animation_track_editor_plugins.h                                      */

#pragma once

#include "editor/animation/animation_track_editor.h"

class AnimationTrackEditBool : public AnimationTrackEdit {
	ORBLIT_CLASS(AnimationTrackEditBool, AnimationTrackEdit);

public:
	virtual int get_key_height() const override;
	virtual Rect2 get_key_rect(int p_index, float p_pixels_sec) override;
	virtual bool is_key_selectable_by_distance() const override;
	virtual void draw_key(int p_index, float p_pixels_sec, int p_x, bool p_selected, int p_clip_left, int p_clip_right) override;
};

class AnimationTrackEditColor : public AnimationTrackEdit {
	ORBLIT_CLASS(AnimationTrackEditColor, AnimationTrackEdit);

public:
	virtual int get_key_height() const override;
	virtual Rect2 get_key_rect(int p_index, float p_pixels_sec) override;
	virtual bool is_key_selectable_by_distance() const override;
	virtual void draw_key(int p_index, float p_pixels_sec, int p_x, bool p_selected, int p_clip_left, int p_clip_right) override;
	virtual void draw_key_link(int p_index_from, int p_index_to, float p_pixels_sec, int p_x, int p_next_x, int p_clip_left, int p_clip_right) override;
};

class AnimationTrackEditAudio : public AnimationTrackEdit {
	ORBLIT_CLASS(AnimationTrackEditAudio, AnimationTrackEdit);

	ObjectID id;

	void _preview_changed(ObjectID p_which);

public:
	virtual int get_key_height() const override;
	virtual Rect2 get_key_rect(int p_index, float p_pixels_sec) override;
	virtual bool is_key_selectable_by_distance() const override;
	virtual void draw_key(int p_index, float p_pixels_sec, int p_x, bool p_selected, int p_clip_left, int p_clip_right) override;

	void set_node(Object *p_object);

	AnimationTrackEditAudio();
};

class AnimationTrackEditSpriteFrame : public AnimationTrackEdit {
	ORBLIT_CLASS(AnimationTrackEditSpriteFrame, AnimationTrackEdit);

	ObjectID id;
	bool is_coords = false;

public:
	virtual int get_key_height() const override;
	virtual Rect2 get_key_rect(int p_index, float p_pixels_sec) override;
	virtual bool is_key_selectable_by_distance() const override;
	virtual void draw_key(int p_index, float p_pixels_sec, int p_x, bool p_selected, int p_clip_left, int p_clip_right) override;

	void set_node(Object *p_object);
	void set_as_coords();
};

class AnimationTrackEditSubAnim : public AnimationTrackEdit {
	ORBLIT_CLASS(AnimationTrackEditSubAnim, AnimationTrackEdit);

	ObjectID id;

public:
	virtual int get_key_height() const override;
	virtual Rect2 get_key_rect(int p_index, float p_pixels_sec) override;
	virtual bool is_key_selectable_by_distance() const override;
	virtual void draw_key(int p_index, float p_pixels_sec, int p_x, bool p_selected, int p_clip_left, int p_clip_right) override;

	void set_node(Object *p_object);
};

class AnimationTrackEditTypeAudio : public AnimationTrackEdit {
	ORBLIT_CLASS(AnimationTrackEditTypeAudio, AnimationTrackEdit);

	void _preview_changed(ObjectID p_which);

	bool len_resizing = false;
	bool len_resizing_start = false;
	int len_resizing_index = 0;
	float len_resizing_from_px = 0.0f;
	float len_resizing_rel = 0.0f;
	bool over_drag_position = false;

public:
	virtual void gui_input(const Ref<InputEvent> &p_event) override;

	virtual bool can_drop_data(const Point2 &p_point, const Variant &p_data) const override;
	virtual void drop_data(const Point2 &p_point, const Variant &p_data) override;

	virtual int get_key_height() const override;
	virtual Rect2 get_key_rect(int p_index, float p_pixels_sec) override;
	virtual bool is_key_selectable_by_distance() const override;
	virtual void draw_key(int p_index, float p_pixels_sec, int p_x, bool p_selected, int p_clip_left, int p_clip_right) override;

	virtual CursorShape get_cursor_shape(const Point2 &p_pos) const override;

	AnimationTrackEditTypeAudio();
};

class AnimationTrackEditTypeAnimation : public AnimationTrackEdit {
	ORBLIT_CLASS(AnimationTrackEditTypeAnimation, AnimationTrackEdit);

	ObjectID id;

public:
	virtual int get_key_height() const override;
	virtual Rect2 get_key_rect(int p_index, float p_pixels_sec) override;
	virtual bool is_key_selectable_by_distance() const override;
	virtual void draw_key(int p_index, float p_pixels_sec, int p_x, bool p_selected, int p_clip_left, int p_clip_right) override;

	void set_node(Object *p_object);
};

class AnimationTrackEditVolumeDB : public AnimationTrackEdit {
	ORBLIT_CLASS(AnimationTrackEditVolumeDB, AnimationTrackEdit);

public:
	virtual void draw_bg(int p_clip_left, int p_clip_right) override;
	virtual void draw_fg(int p_clip_left, int p_clip_right) override;
	virtual int get_key_height() const override;
	virtual void draw_key_link(int p_index_from, int p_index_to, float p_pixels_sec, int p_x, int p_next_x, int p_clip_left, int p_clip_right) override;
};

class AnimationTrackEditDefaultPlugin : public AnimationTrackEditPlugin {
	ORBLIT_CLASS(AnimationTrackEditDefaultPlugin, AnimationTrackEditPlugin);

public:
	virtual AnimationTrackEdit *create_value_track_edit(Object *p_object, Variant::Type p_type, const String &p_property, PropertyHint p_hint, const String &p_hint_string, int p_usage) override;
	virtual AnimationTrackEdit *create_audio_track_edit() override;
	virtual AnimationTrackEdit *create_animation_track_edit(Object *p_object) override;
};
