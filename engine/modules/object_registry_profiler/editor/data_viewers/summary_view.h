/**************************************************************************/
/*  summary_view.h                                                        */

#pragma once

#include "../snapshot_data.h"
#include "snapshot_view.h"

#include "scene/ui/margin_container.h"

class CenterContainer;
class RichTextLabel;

class SummaryBlurb : public MarginContainer {
	ORBLIT_CLASS(SummaryBlurb, MarginContainer);

public:
	RichTextLabel *label = nullptr;

	SummaryBlurb(const String &p_title, const String &p_rtl_content);
};

class SnapshotSummaryView : public SnapshotView {
	ORBLIT_CLASS(SnapshotSummaryView, SnapshotView);

	PanelContainer *content_wrapper = nullptr;
	Label *title = nullptr;

	VBoxContainer *blurb_list = nullptr;
	CenterContainer *explainer_text = nullptr;

	void _push_overview_blurb(const String &p_title, GameStateSnapshot *p_snapshot);
	void _push_node_blurb(const String &p_title, GameStateSnapshot *p_snapshot);
	void _push_refcounted_blurb(const String &p_title, GameStateSnapshot *p_snapshot);
	void _push_object_blurb(const String &p_title, GameStateSnapshot *p_snapshot);

protected:
	void _notification(int p_what);

public:
	SnapshotSummaryView();

	virtual void show_snapshot(GameStateSnapshot *p_data, GameStateSnapshot *p_diff_data) override;
	virtual void clear_snapshot() override;
};
