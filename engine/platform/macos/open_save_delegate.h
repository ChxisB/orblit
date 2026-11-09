/**************************************************************************/
/*  open_save_delegate.h                                                  */

#pragma once

#include "core/containers/hash_map.h"
#include "core/variant/typed_array.h"
#include "core/variant/variant.h"

#import <AppKit/AppKit.h>
#import <Foundation/Foundation.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

@interface OrblitOpenSaveDelegate : NSObject <NSOpenSavePanelDelegate> {
	NSSavePanel *dialog;
	NSMutableArray *allowed_types;
	Vector<Vector<String>> preferred_types;

	HashMap<int, String> ctr_ids;
	Dictionary options;
	int cur_index;
	int ctr_id;

	String root;
}

- (void)makeAccessoryView:(NSSavePanel *)p_panel filters:(const Vector<String> &)p_filters options:(const TypedArray<Dictionary> &)p_options;
- (void)setFileTypes:(NSMutableArray *)p_allowed_types pref:(const Vector<Vector<String>> &)p_preftypes;
- (void)popupOptionAction:(id)p_sender;
- (void)popupCheckAction:(id)p_sender;
- (void)popupFileAction:(id)p_sender;
- (int)getIndex;
- (Dictionary)getSelection;
- (int)setDefaultInt:(const String &)p_name value:(int)p_value;
- (int)setDefaultBool:(const String &)p_name value:(bool)p_value;
- (void)setRootPath:(const String &)p_root_path;
- (String)validateFilename:(const String &)p_path;

@end
