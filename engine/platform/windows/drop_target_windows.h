/**************************************************************************/
/*  drop_target_windows.h                                                 */

#pragma once

#include "display_system_windows.h"

#include <shlobj.h>

ORBLIT_GCC_WARNING_PUSH_AND_IGNORE("-Wnon-virtual-dtor") // Silence warning due to a COM API weirdness.

// https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-dodragdrop#remarks
class DropTargetWindows : public IDropTarget {
	LONG ref_count;
	DisplaySystemWindows::WindowData *window_data = nullptr;
	CLIPFORMAT cf_filedescriptor = 0;
	CLIPFORMAT cf_filecontents = 0;
	String tmp_path;

	bool is_valid_filedescriptor();
	HRESULT handle_hdrop_format(Vector<String> *p_files, IDataObject *pDataObj);
	HRESULT handle_filedescriptor_format(Vector<String> *p_files, IDataObject *pDataObj);
	HRESULT save_as_file(const String &p_out_dir, FILEDESCRIPTORW *p_file_desc, IDataObject *pDataObj, int p_file_idx);

public:
	DropTargetWindows(DisplaySystemWindows::WindowData *p_window_data);
	virtual ~DropTargetWindows() {}

	// IUnknown
	HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void **ppvObject) override;
	ULONG STDMETHODCALLTYPE AddRef() override;
	ULONG STDMETHODCALLTYPE Release() override;

	// IDropTarget
	HRESULT STDMETHODCALLTYPE DragEnter(IDataObject *pDataObj, DWORD grfKeyState, POINTL pt, DWORD *pdwEffect) override;
	HRESULT STDMETHODCALLTYPE DragOver(DWORD grfKeyState, POINTL pt, DWORD *pdwEffect) override;
	HRESULT STDMETHODCALLTYPE DragLeave() override;
	HRESULT STDMETHODCALLTYPE Drop(IDataObject *pDataObj, DWORD grfKeyState, POINTL pt, DWORD *pdwEffect) override;
};

ORBLIT_GCC_WARNING_POP
