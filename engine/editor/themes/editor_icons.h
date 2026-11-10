/**************************************************************************/
/*  editor_icons.h                                                        */

#pragma once

#include "scene/resources/theme.h"

void editor_configure_icons(bool p_dark_icon_and_font);
void editor_register_icons(const Ref<Theme> &p_theme, bool p_dark_theme, float p_icon_saturation, int p_thumb_size, float p_gizmo_handle_scale);
void editor_copy_icons(const Ref<Theme> &p_theme, const Ref<Theme> &p_old_theme);

String get_default_project_icon();
