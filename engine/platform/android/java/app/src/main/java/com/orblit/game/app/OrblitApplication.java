/**************************************************************************/
/*  OrblitApplication.java                                                */

package com.orblit.game.app;

import android.app.Application;
import android.util.Log;

public class OrblitApplication extends Application {
	static {
		// .NET libraries.
		if (BuildConfig.FLAVOR.equals("mono")) {
			try {
				Log.v("ORBLIT", "Loading System.Security.Cryptography.Native.Android library");
				System.loadLibrary("System.Security.Cryptography.Native.Android");
			} catch (UnsatisfiedLinkError e) {
				Log.e("ORBLIT", "Unable to load System.Security.Cryptography.Native.Android library");
			}
		}
	}
}
