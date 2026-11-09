/**************************************************************************/
/*  winrt_utils.h                                                         */

#pragma once

#include "winrt_defines.h"

#include "core/typedefs.h"
#include "core/variant/callable.h"
#include "core/variant/variant.h"
#include "systems/display/display_system_enums.h"

#include <windows.h>

class WinRTWindowData;
class Texture2D;

class WinRTUtils {
public:
	static bool is_api_contract_present(const String &p_contract, uint16_t p_version);
	static bool is_type_present(const String &p_type);
	static HRESULT activation_factory(const String &p_class_name, REFIID p_iid, void **p_factory);

	static bool is_initialized();

	static void init();
	static bool create_queue(const String &p_appid);
	static void destroy_queue();
	static void cleanup();

	static bool try_show_onecore_emoji_picker();
	static Vector<String> get_preferred_locales();

	static WinRTWindowData *create_wd(HWND p_window, const Callable &p_color_cb, int64_t p_window_id);
	static bool window_has_display_info(const WinRTWindowData *p_data);
	static void window_get_advanced_color_info(const WinRTWindowData *p_data, bool &r_hdr_supported, float &r_min_luminance, float &r_max_luminance, float &r_max_average_luminance, float &r_sdr_white_level);
	static void destroy_wd(WinRTWindowData *p_data);

	static DisplaySystemEnums::NotificationID send_toast_notification(const String &p_title, const String &p_text, const Ref<Texture2D> &p_image, const Callable &p_callback);
	static void hide_toast_notification(DisplaySystemEnums::NotificationID p_id);
};
