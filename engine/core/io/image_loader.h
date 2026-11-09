/**************************************************************************/
/*  image_loader.h                                                        */

#pragma once

#include "core/containers/list.h"
#include "core/io/file_access.h"
#include "core/io/image.h"
#include "core/object/orblit_virtual.gen.h"
#include "core/string/ustring.h"
#include "core/variant/type_info.h"

class ImageLoader;

class ImageFormatLoader : public RefCounted {
	ORBLIT_CLASS(ImageFormatLoader, RefCounted);

	friend class ImageLoader;
	friend class ResourceFormatLoaderImage;

public:
	enum LoaderFlags {
		FLAG_NONE = 0,
		FLAG_FORCE_LINEAR = 1,
		FLAG_CONVERT_COLORS = 2,
	};

protected:
	static void register_members();

	virtual Error load_image(Ref<Image> p_image, Ref<FileAccess> p_fileaccess, BitField<ImageFormatLoader::LoaderFlags> p_flags = FLAG_NONE, float p_scale = 1.0) = 0;
	virtual void get_recognized_extensions(List<String> *p_extensions) const = 0;
	bool recognize(const String &p_extension) const;

public:
	virtual ~ImageFormatLoader() {}
};

VARIANT_BITFIELD_CAST(ImageFormatLoader::LoaderFlags);

class ImageFormatLoaderExtension : public ImageFormatLoader {
	ORBLIT_CLASS(ImageFormatLoaderExtension, ImageFormatLoader);

protected:
	static void register_members();

public:
	virtual Error load_image(Ref<Image> p_image, Ref<FileAccess> p_fileaccess, BitField<ImageFormatLoader::LoaderFlags> p_flags = FLAG_NONE, float p_scale = 1.0) override;
	virtual void get_recognized_extensions(List<String> *p_extensions) const override;

	void add_format_loader();
	void remove_format_loader();

	ORBLIT_VIRTUAL0RC(PackedStringArray, _get_recognized_extensions);
	ORBLIT_VIRTUAL4R(Error, _load_image, Ref<Image>, Ref<FileAccess>, BitField<ImageFormatLoader::LoaderFlags>, float);
};

class ImageLoader {
	static inline Vector<Ref<ImageFormatLoader>> loader;
	friend class ResourceFormatLoaderImage;

protected:
public:
	static Error load_image(const String &p_file, Ref<Image> p_image, Ref<FileAccess> p_custom = Ref<FileAccess>(), BitField<ImageFormatLoader::LoaderFlags> p_flags = ImageFormatLoader::FLAG_NONE, float p_scale = 1.0);
	static void get_recognized_extensions(List<String> *p_extensions);
	static Ref<ImageFormatLoader> recognize(const String &p_extension);

	static void add_image_format_loader(Ref<ImageFormatLoader> p_loader);
	static void remove_image_format_loader(Ref<ImageFormatLoader> p_loader);

	static void cleanup();
};
