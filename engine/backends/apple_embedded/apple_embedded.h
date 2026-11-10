/**************************************************************************/
/*  apple_embedded.h                                                      */

#pragma once

#include "core/object/object.h"

#import <CoreHaptics/CoreHaptics.h>

class AppleEmbedded : public Object {
	ORBLIT_CLASS(AppleEmbedded, Object);

	static void register_members();

private:
	CHHapticEngine *haptic_engine API_AVAILABLE(ios(13)) = nullptr;

	CHHapticEngine *get_haptic_engine_instance() API_AVAILABLE(ios(13));
	void start_haptic_engine();
	void stop_haptic_engine();

public:
	static void alert(const char *p_alert, const char *p_title);

	bool supports_haptic_engine();
	void vibrate_haptic_engine(float p_duration_seconds, float p_amplitude);

	String get_model() const;
	String get_rate_url(int p_app_id) const;

	AppleEmbedded();
};
