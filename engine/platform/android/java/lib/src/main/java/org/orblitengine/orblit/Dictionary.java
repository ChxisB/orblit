/**************************************************************************/
/*  Dictionary.java                                                       */

package org.orblitengine.orblit;

import java.util.HashMap;
import java.util.Map;
import java.util.Set;

public final class Dictionary extends HashMap<String, Object> {
	private String[] keys_cache;

	/**
	 * @deprecated Use {@link java.util.HashMap#keySet()} instead.
	 */
	@Deprecated
	public String[] get_keys() {
		String[] ret = new String[size()];
		int i = 0;
		Set<String> keys = keySet();
		for (String key : keys) {
			ret[i] = key;
			i++;
		}

		return ret;
	}

	/**
	 * @deprecated Use {@link java.util.HashMap#values()} instead.
	 */
	@Deprecated
	public Object[] get_values() {
		Object[] ret = new Object[size()];
		int i = 0;
		Set<String> keys = keySet();
		for (String key : keys) {
			ret[i] = get(key);
			i++;
		}

		return ret;
	}

	/**
	 * @deprecated Use {@link java.util.HashMap#putAll(Map)} instead.
	 */
	@Deprecated
	public void set_keys(String[] keys) {
		keys_cache = keys;
	}

	/**
	 * @deprecated Use {@link java.util.HashMap#putAll(Map)} instead.
	 */
	@Deprecated
	public void set_values(Object[] vals) {
		int i = 0;
		for (String key : keys_cache) {
			put(key, vals[i]);
			i++;
		}
		keys_cache = null;
	}
}
