/**************************************************************************/
/*  separator.cpp                                                         */

#include "separator.h"

#include "scene/theme/theme_db.h"

Size2 Separator::get_minimum_size() const {
	Size2 ms(3, 3);
	if (orientation == VERTICAL) {
		ms.x = theme_cache.separation;
	} else { // HORIZONTAL
		ms.y = theme_cache.separation;
	}
	return ms;
}

void Separator::_notification(int p_what) {
	switch (p_what) {
		case NOTIFICATION_DRAW: {
			Size2i size = get_size();
			Size2i ssize = theme_cache.separator_style->get_minimum_size();

			if (orientation == VERTICAL) {
				theme_cache.separator_style->draw(get_canvas_item(), Rect2((size.x - ssize.x) / 2, 0, ssize.x, size.y));
			} else {
				theme_cache.separator_style->draw(get_canvas_item(), Rect2(0, (size.y - ssize.y) / 2, size.x, ssize.y));
			}
		} break;
	}
}

void Separator::register_members() {
	BIND_THEME_ITEM(Theme::DATA_TYPE_CONSTANT, Separator, separation);
	BIND_THEME_ITEM_CUSTOM(Theme::DATA_TYPE_STYLEBOX, Separator, separator_style, "separator");
}

Separator::Separator() {
}

Separator::~Separator() {
}

HSeparator::HSeparator() {
	orientation = HORIZONTAL;
}

VSeparator::VSeparator() {
	orientation = VERTICAL;
}
