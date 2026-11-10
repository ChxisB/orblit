/**************************************************************************/
/*  EditorWindowInfo.kt                                                   */

package org.orblitengine.editor

/**
 * Specifies the policy for launches.
 */
enum class LaunchPolicy {
	/**
	 * Launch policy is determined by the editor settings or based on the device and screen metrics.
	 */
	AUTO,


	/**
	 * Launches happen in the same window.
	 */
	SAME,

	/**
	 * Adjacent launches are enabled.
	 */
	ADJACENT
}

/**
 * Describe the editor window to launch
 */
data class EditorWindowInfo(
	val windowClassName: String,
	val windowId: Int,
	val processNameSuffix: String,
	val launchPolicy: LaunchPolicy = LaunchPolicy.SAME
) {
	constructor(
		windowClass: Class<*>,
		windowId: Int,
		processNameSuffix: String,
		launchPolicy: LaunchPolicy = LaunchPolicy.SAME
	) : this(windowClass.name, windowId, processNameSuffix, launchPolicy)
}
