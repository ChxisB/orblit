/**************************************************************************/
/*  text_file.h                                                           */

#pragma once

#include "core/io/resource.h"

class TextFile : public Resource {
	ORBLIT_CLASS(TextFile, Resource);

private:
	String text;
	String path;

protected:
	virtual bool editor_can_reload_from_file() override { return false; }

public:
	virtual bool has_text() const;
	virtual String get_text() const;
	virtual void set_text(const String &p_code);
	virtual void reload_from_file() override;

	void set_file_path(const String &p_path) { path = p_path; }
	Error load_text(const String &p_path);
};
