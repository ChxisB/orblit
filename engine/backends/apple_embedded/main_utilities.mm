/**************************************************************************/
/*  main_utilities.mm                                                     */

#include "core/string/ustring.h"

#import <UIKit/UIKit.h>
#include <unistd.h>

#include <cstdio>

void change_to_launch_dir(char **p_args) {
	size_t len = strlen(p_args[0]);

	while (len--) {
		if (p_args[0][len] == '/') {
			break;
		}
	}

	if (len >= 0) {
		char path[512];
		memcpy(path, p_args[0], len > sizeof(path) ? sizeof(path) : len);
		path[len] = 0;
		chdir(path);
	}
}

int add_cmdline(int p_argc, char **p_args) {
	NSArray *arr = [[[NSBundle mainBundle] infoDictionary] objectForKey:@"orblit_cmdline"];
	if (!arr) {
		return p_argc;
	}

	for (NSUInteger i = 0; i < [arr count]; i++) {
		NSString *str = [arr objectAtIndex:i];
		if (!str) {
			continue;
		}
		p_args[p_argc++] = (char *)[str cStringUsingEncoding:NSUTF8StringEncoding];
	}

	p_args[p_argc] = nullptr;

	return p_argc;
}

int process_args(int p_argc, char **p_args, char **r_args) {
	for (int i = 0; i < p_argc; i++) {
		r_args[i] = p_args[i];
	}
	r_args[p_argc] = nullptr;
	p_argc = add_cmdline(p_argc, r_args);
	return p_argc;
}
