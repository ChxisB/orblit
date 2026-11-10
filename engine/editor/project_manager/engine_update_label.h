/**************************************************************************/
/*  engine_update_label.h                                                 */

#pragma once

#include "scene/ui/link_button.h"

class HTTPRequest;

class EngineUpdateLabel : public LinkButton {
	ORBLIT_CLASS(EngineUpdateLabel, LinkButton);

public:
	enum class UpdateMode {
		DISABLED,
		AUTO,
		NEWEST_UNSTABLE,
		NEWEST_STABLE,
		NEWEST_PATCH,
	};

private:
	static constexpr int DEV_VERSION = 9999; // Version index for unnumbered builds (assumed to always be newest).

	enum class VersionType {
		STABLE,
		RC,
		BETA,
		ALPHA,
		DEV,
		UNKNOWN,
	};

	enum class UpdateStatus {
		NONE,
		OFFLINE,
		BUSY,
		ERROR,
		UPDATE_AVAILABLE,
		UP_TO_DATE,
	};

	struct ThemeCache {
		Color default_color;
		Color disabled_color;
		Color error_color;
		Color update_color;
	} theme_cache;

	HTTPRequest *http = nullptr;

	UpdateStatus status = UpdateStatus::NONE;
	bool checked_update = false;
	String available_newer_version;

	String current_message;
	Variant message_argument;

	bool _can_check_updates() const;
	void _check_update();
	void _http_request_completed(int p_result, int p_response_code, const PackedStringArray &p_headers, const PackedByteArray &p_body);

	void _set_message(const String &p_message, const Color &p_color, const Variant &p_message_argument = Variant());
	void _set_status(UpdateStatus p_status);
	void _update_message();

	VersionType _get_version_type(const String &p_string, int *r_index = nullptr) const;
	String _extract_sub_string(const String &p_line) const;

protected:
	void _notification(int p_what);
	static void register_members();

	virtual void pressed() override;

public:
	EngineUpdateLabel();
};
