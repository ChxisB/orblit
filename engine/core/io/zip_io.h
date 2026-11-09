/**************************************************************************/
/*  zip_io.h                                                              */

#pragma once

#include "core/io/file_access.h"

// This file serves as the Orblit interface to minizip.
#include <thirdparty/minizip/unzip.h> // IWYU pragma: export
#include <thirdparty/minizip/zip.h> // IWYU pragma: export

// Get the current file info and safely convert the full filepath to a String.
int orblit_unzip_get_current_file_info(unzFile p_zip_file, unz_file_info64 &r_file_info, String &r_filepath);
// Try to locate the file in the archive specified by the filepath (works with large paths and Unicode).
int orblit_unzip_locate_file(unzFile p_zip_file, const String &p_filepath, bool p_case_sensitive = true);

//

void *zipio_open(voidpf p_opaque, const char *p_fname, int p_mode);
uLong zipio_read(voidpf p_opaque, voidpf p_stream, void *p_buf, uLong p_size);
uLong zipio_write(voidpf p_opaque, voidpf p_stream, const void *p_buf, uLong p_size);

long zipio_tell(voidpf p_opaque, voidpf p_stream);
long zipio_seek(voidpf p_opaque, voidpf p_stream, uLong p_offset, int p_origin);

int zipio_close(voidpf p_opaque, voidpf p_stream);

int zipio_testerror(voidpf p_opaque, voidpf p_stream);

voidpf zipio_alloc(voidpf p_opaque, uInt p_items, uInt p_size);
void zipio_free(voidpf p_opaque, voidpf p_address);

zlib_filefunc_def zipio_create_io(Ref<FileAccess> *p_data);
