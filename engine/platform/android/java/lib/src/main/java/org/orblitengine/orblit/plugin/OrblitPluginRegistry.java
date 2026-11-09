/**************************************************************************/
/*  OrblitPluginRegistry.java                                             */

package org.orblitengine.orblit.plugin;

import org.orblitengine.orblit.Orblit;

import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.os.Bundle;
import android.text.TextUtils;
import android.util.Log;

import androidx.annotation.Nullable;

import java.lang.reflect.Constructor;
import java.util.Collection;
import java.util.Collections;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;

/**
 * Registry used to load and access the registered Orblit Android plugins.
 */
public final class OrblitPluginRegistry {
	private static final String TAG = OrblitPluginRegistry.class.getSimpleName();

	/**
	 * Prefix used for version 1 of the Orblit plugin, mostly compatible with Orblit 3.x
	 */
	private static final String ORBLIT_PLUGIN_V1_NAME_PREFIX = "org.orblitengine.plugin.v1.";
	/**
	 * Prefix used for version 2 of the Orblit plugin, compatible with Orblit 4.2+
	 */
	private static final String ORBLIT_PLUGIN_V2_NAME_PREFIX = "org.orblitengine.plugin.v2.";

	private static OrblitPluginRegistry instance;
	private final ConcurrentHashMap<String, OrblitPlugin> registry;

	private OrblitPluginRegistry() {
		registry = new ConcurrentHashMap<>();
	}

	/**
	 * Retrieve the plugin tied to the given plugin name.
	 * @param pluginName Name of the plugin
	 * @return {@link OrblitPlugin} handle if it exists, null otherwise.
	 */
	@Nullable
	public OrblitPlugin getPlugin(String pluginName) {
		return registry.get(pluginName);
	}

	/**
	 * Retrieve the full set of loaded plugins.
	 */
	public Collection<OrblitPlugin> getAllPlugins() {
		if (registry.isEmpty()) {
			return Collections.emptyList();
		}
		return registry.values();
	}

	/**
	 * Parse the manifest file and load all included Orblit Android plugins.
	 * <p>
	 * A plugin manifest entry is a '<meta-data>' tag setup as described in the {@link OrblitPlugin}
	 * documentation.
	 *
	 * @param orblit Orblit instance
	 * @param runtimePlugins Set of plugins provided at runtime for registration
	 * @return A singleton instance of {@link OrblitPluginRegistry}. This ensures that only one instance
	 * of each Orblit Android plugins is available at runtime.
	 */
	public static OrblitPluginRegistry initializePluginRegistry(Orblit orblit, Set<OrblitPlugin> runtimePlugins) {
		if (instance == null) {
			instance = new OrblitPluginRegistry();
			instance.loadPlugins(orblit, runtimePlugins);
		}

		return instance;
	}

	/**
	 * Return the plugin registry if it's initialized.
	 * Throws a {@link IllegalStateException} exception if not.
	 *
	 * @throws IllegalStateException if {@link OrblitPluginRegistry#initializePluginRegistry(Orblit, Set)} has not been called prior to calling this method.
	 */
	public static OrblitPluginRegistry getPluginRegistry() throws IllegalStateException {
		if (instance == null) {
			throw new IllegalStateException("Plugin registry hasn't been initialized.");
		}

		return instance;
	}

	private void loadPlugins(Orblit orblit, Set<OrblitPlugin> runtimePlugins) {
		// Register the runtime plugins
		if (runtimePlugins != null && !runtimePlugins.isEmpty()) {
			for (OrblitPlugin plugin : runtimePlugins) {
				Log.i(TAG, "Registering runtime plugin " + plugin.getPluginName());
				registry.put(plugin.getPluginName(), plugin);
			}
		}

		// Register the manifest plugins
		try {
			final Context context = orblit.getContext();
			ApplicationInfo appInfo = context
											  .getPackageManager()
											  .getApplicationInfo(context.getPackageName(),
													  PackageManager.GET_META_DATA);
			Bundle metaData = appInfo.metaData;
			if (metaData == null || metaData.isEmpty()) {
				return;
			}

			for (String metaDataName : metaData.keySet()) {
				// Parse the meta-data looking for entry with the Orblit plugin name prefix.
				String pluginName = null;
				if (metaDataName.startsWith(ORBLIT_PLUGIN_V2_NAME_PREFIX)) {
					pluginName = metaDataName.substring(ORBLIT_PLUGIN_V2_NAME_PREFIX.length()).trim();
				} else if (metaDataName.startsWith(ORBLIT_PLUGIN_V1_NAME_PREFIX)) {
					pluginName = metaDataName.substring(ORBLIT_PLUGIN_V1_NAME_PREFIX.length()).trim();
					Log.w(TAG, "Orblit v1 plugin are deprecated in Orblit 4.2 and higher: " + pluginName);
				}

				if (!TextUtils.isEmpty(pluginName)) {
					Log.i(TAG, "Initializing Orblit plugin " + pluginName);

					// Retrieve the plugin class full name.
					String pluginHandleClassFullName = metaData.getString(metaDataName);
					if (!TextUtils.isEmpty(pluginHandleClassFullName)) {
						try {
							// Attempt to create the plugin init class via reflection.
							@SuppressWarnings("unchecked")
							Class<OrblitPlugin> pluginClass = (Class<OrblitPlugin>)Class
																	 .forName(pluginHandleClassFullName);
							Constructor<OrblitPlugin> pluginConstructor = pluginClass
																				 .getConstructor(Orblit.class);
							OrblitPlugin pluginHandle = pluginConstructor.newInstance(orblit);

							// Load the plugin initializer into the registry using the plugin name as key.
							if (!pluginName.equals(pluginHandle.getPluginName())) {
								Log.w(TAG,
										"Meta-data plugin name does not match the value returned by the plugin handle: " + pluginName + " =/= " + pluginHandle.getPluginName());
							}
							registry.put(pluginName, pluginHandle);
							Log.i(TAG, "Completed initialization for Orblit plugin " + pluginHandle.getPluginName());
						} catch (Exception e) {
							Log.w(TAG, "Unable to load Orblit plugin " + pluginName, e);
						}
					} else {
						Log.w(TAG, "Invalid plugin loader class for " + pluginName);
					}
				}
			}
		} catch (Exception e) {
			Log.e(TAG, "Unable load Orblit Android plugins from the manifest file.", e);
		}
	}
}
