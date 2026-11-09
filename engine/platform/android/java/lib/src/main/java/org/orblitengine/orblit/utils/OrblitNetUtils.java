/**************************************************************************/
/*  OrblitNetUtils.java                                                   */

package org.orblitengine.orblit.utils;

import android.app.Activity;
import android.content.Context;
import android.net.wifi.WifiManager;
import android.util.Base64;
import android.util.Log;

import androidx.annotation.NonNull;

import java.security.KeyStore;
import java.security.cert.X509Certificate;
import java.util.Enumeration;

/**
 * This class handles Android-specific networking functions.
 * It provides access to the CA certificates KeyStore, and the WifiManager.MulticastLock, which is needed on some devices
 * to receive broadcast and multicast packets.
 */
public class OrblitNetUtils {
	/* A single, reference counted, multicast lock, or null if permission CHANGE_WIFI_MULTICAST_STATE is missing */
	private WifiManager.MulticastLock multicastLock;

	public OrblitNetUtils(Context context) {
		if (PermissionsUtil.hasManifestPermission(context, "android.permission.CHANGE_WIFI_MULTICAST_STATE")) {
			WifiManager wifi = (WifiManager)context.getApplicationContext().getSystemService(Context.WIFI_SERVICE);
			multicastLock = wifi.createMulticastLock("OrblitMulticastLock");
			multicastLock.setReferenceCounted(true);
		}
	}

	/**
	 * Acquire the multicast lock. This is required on some devices to receive broadcast/multicast packets.
	 * This is done automatically by Orblit when enabling broadcast or joining a multicast group on a socket.
	 */
	public void multicastLockAcquire() {
		if (multicastLock == null)
			return;
		try {
			multicastLock.acquire();
		} catch (RuntimeException e) {
			Log.e("Orblit", "Exception during multicast lock acquire: " + e);
		}
	}

	/**
	 * Release the multicast lock.
	 * This is done automatically by Orblit when the lock is no longer needed by a socket.
	 */
	public void multicastLockRelease() {
		if (multicastLock == null)
			return;
		try {
			multicastLock.release();
		} catch (RuntimeException e) {
			Log.e("Orblit", "Exception during multicast lock release: " + e);
		}
	}

	/**
	 * Retrieves the list of trusted CA certificates from the "AndroidCAStore" and returns them in PRM format.
	 * @see https://developer.android.com/reference/java/security/KeyStore .
	 * @return A string of concatenated X509 certificates in PEM format.
	 */
	public static @NonNull String getCACertificates() {
		try {
			KeyStore ks = KeyStore.getInstance("AndroidCAStore");
			StringBuilder writer = new StringBuilder();

			if (ks != null) {
				ks.load(null, null);
				Enumeration<String> aliases = ks.aliases();

				while (aliases.hasMoreElements()) {
					String alias = (String)aliases.nextElement();

					X509Certificate cert = (X509Certificate)ks.getCertificate(alias);
					writer.append("-----BEGIN CERTIFICATE-----\n");
					writer.append(Base64.encodeToString(cert.getEncoded(), Base64.DEFAULT));
					writer.append("-----END CERTIFICATE-----\n");
				}
			}
			return writer.toString();
		} catch (Exception e) {
			Log.e("Orblit", "Exception while reading CA certificates: " + e);
			return "";
		}
	}
}
