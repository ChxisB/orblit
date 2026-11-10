/**************************************************************************/
/*  foldable_container.h                                                  */

#pragma once

#include "scene/ui/container.h"

class FoldableGroup;
class TextLine;

class FoldableContainer : public Container {
	ORBLIT_CLASS(FoldableContainer, Container);

public:
	enum TitlePosition {
		POSITION_TOP,
		POSITION_BOTTOM,
		POSITION_MAX
	};

private:
	bool folded = false;
	String title;
	Ref<FoldableGroup> foldable_group;
	String language;
	TextDirection title_text_direction = TEXT_DIRECTION_AUTO;
	HorizontalAlignment title_alignment = HORIZONTAL_ALIGNMENT_LEFT;
	TextSystem::OverrunBehavior overrun_behavior = TextSystem::OVERRUN_NO_TRIMMING;
	TitlePosition title_position = POSITION_TOP;

	Ref<TextLine> text_buf;
	bool changing_group = false;
	bool is_hovering = false;
	mutable Vector2 title_minimum_size;

	LocalVector<Control *> title_controls;

	struct ThemeCache {
		Ref<StyleBox> title_style;
		Ref<StyleBox> title_hover_style;
		Ref<StyleBox> title_collapsed_style;
		Ref<StyleBox> title_collapsed_hover_style;
		Ref<StyleBox> panel_style;
		Ref<StyleBox> focus_style;

		Color title_font_color;
		Color title_hovered_font_color;
		Color title_collapsed_font_color;
		Color title_font_outline_color;

		Ref<Font> title_font;
		int title_font_size = 0;
		int title_font_outline_size = 0;

		Ref<Texture2D> expanded_arrow;
		Ref<Texture2D> expanded_arrow_mirrored;
		Ref<Texture2D> folded_arrow;
		Ref<Texture2D> folded_arrow_mirrored;

		int h_separation = 0;
		int icon_max_width = 0;

		Ref<AudioStream> focus_sound;
		Ref<AudioStream> expanded_sound;
		Ref<AudioStream> folded_sound;
	} theme_cache;

	Ref<StyleBox> _get_title_style() const;
	Ref<Texture2D> _get_title_icon() const;
	Rect2 _get_title_rect() const;
	int _get_h_separation() const { return MAX(theme_cache.h_separation, 0); }
	real_t _get_title_controls_width() const;

	void _update_title_min_size() const;
	void _shape();
	HorizontalAlignment _get_actual_alignment() const;
	void _update_group();
	void _draw_flippable_stylebox(const Ref<StyleBox> p_stylebox, const Rect2 &p_rect);

protected:
	virtual void gui_input(const Ref<InputEvent> &p_event) override;
	virtual String get_tooltip(const Point2 &p_pos) const override;
	virtual bool has_point(const Point2 &p_point) const override;
	void _notification(int p_what);
	Size2 _fit_icon_size(const Size2 &p_size) const;
	static void register_members();

public:
	void fold();
	void expand();

	void set_folded(bool p_folded);
	bool is_folded() const;

	void set_foldable_group(const Ref<FoldableGroup> &p_group);
	Ref<FoldableGroup> get_foldable_group() const;

	void set_title(const String &p_text);
	String get_title() const;

	void set_title_alignment(HorizontalAlignment p_alignment);
	HorizontalAlignment get_title_alignment() const;

	void set_title_text_direction(TextDirection p_text_direction);
	TextDirection get_title_text_direction() const;

	void set_title_text_overrun_behavior(TextSystem::OverrunBehavior p_overrun_behavior);
	TextSystem::OverrunBehavior get_title_text_overrun_behavior() const;

	void set_language(const String &p_language);
	String get_language() const;

	void set_title_position(TitlePosition p_title_position);
	TitlePosition get_title_position() const;

	void add_title_bar_control(Control *p_control);
	void remove_title_bar_control(Control *p_control);
	Control *get_title_bar_control(int64_t p_index) const;
	int get_title_bar_control_count() const;

	virtual Size2 get_minimum_size() const override;
	virtual Size2 get_desired_size() const override;
	virtual Size2 get_inner_combined_maximum_size() const override;

	virtual Vector<int> get_allowed_size_flags_horizontal() const override { return { SIZE_FILL, SIZE_SHRINK_BEGIN, SIZE_SHRINK_CENTER, SIZE_SHRINK_END, SIZE_MAXIMIZE }; }
	virtual Vector<int> get_allowed_size_flags_vertical() const override { return { SIZE_FILL, SIZE_SHRINK_BEGIN, SIZE_SHRINK_CENTER, SIZE_SHRINK_END, SIZE_MAXIMIZE }; }

	FoldableContainer(const String &p_text = String());
	~FoldableContainer();
};

VARIANT_ENUM_CAST(FoldableContainer::TitlePosition);

class FoldableGroup : public Resource {
	ORBLIT_CLASS(FoldableGroup, Resource);

	friend class FoldableContainer;

	HashSet<FoldableContainer *> containers;
	bool allow_folding_all = false;
	bool updating_group = false;

protected:
	static void register_members();

public:
	FoldableContainer *get_expanded_container() const;

	void get_containers(List<FoldableContainer *> *r_containers) const;
	TypedArray<FoldableContainer> _get_containers() const;

	void set_allow_folding_all(bool p_enabled);
	bool is_allow_folding_all() const;

	FoldableGroup();
};
