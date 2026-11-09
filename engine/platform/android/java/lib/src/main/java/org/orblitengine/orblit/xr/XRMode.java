/**************************************************************************/
/*  XRMode.java                                                           */

package org.orblitengine.orblit.xr;

/**
 * Orblit available XR modes.
 */
public enum XRMode {
	REGULAR(0, "Regular", "--xr_mode_regular", "Default Android Gamepad"), // Regular/flatscreen
	OPENXR(1, "OpenXR", "--xr_mode_openxr", "");

	final int index;
	final String label;
	public final String cmdLineArg;
	public final String inputFallbackMapping;

	XRMode(int index, String label, String cmdLineArg, String inputFallbackMapping) {
		this.index = index;
		this.label = label;
		this.cmdLineArg = cmdLineArg;
		this.inputFallbackMapping = inputFallbackMapping;
	}
}
