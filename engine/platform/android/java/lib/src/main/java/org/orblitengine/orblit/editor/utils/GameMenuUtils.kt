/**************************************************************************/
/*  GameMenuUtils.kt                                                      */

package org.orblitengine.orblit.editor.utils

import android.util.Log
import org.orblitengine.orblit.OrblitLib

/**
 * Utility class for accessing and using game menu APIs.
 *
 * This class is only functional on editor builds.
 */
object GameMenuUtils {
	private val TAG = GameMenuUtils::class.java.simpleName

	/**
	 * Enum representing the "run/window_placement/game_embed_mode" editor settings.
 	 */
	enum class GameEmbedMode(internal val nativeValue: Int) {
		DISABLED(-1), AUTO(0), ENABLED(1);

		companion object {
			internal const val SETTING_KEY = "run/window_placement/game_embed_mode"

			@JvmStatic
			internal fun fromNativeValue(nativeValue: Int): GameEmbedMode? {
				for (mode in GameEmbedMode.entries) {
					if (mode.nativeValue == nativeValue) {
						return mode
					}
				}
				return null
			}
		}
	}

	@JvmStatic
	external fun setSuspend(enabled: Boolean)

	@JvmStatic
	external fun nextFrame()

	@JvmStatic
	external fun setNodeType(type: Int)

	@JvmStatic
	external fun setSelectMode(mode: Int)

	@JvmStatic
	external fun setSelectionVisible(visible: Boolean)

	@JvmStatic
	external fun setSelectionAvoidLocked(enabled: Boolean)

	@JvmStatic
	external fun setSelectionPreferGroup(enabled: Boolean)

	@JvmStatic
	external fun setCameraOverride(enabled: Boolean)

	@JvmStatic
	external fun setCameraManipulateMode(mode: Int)

	@JvmStatic
	external fun resetCamera2DPosition()

	@JvmStatic
	external fun resetCamera3DPosition()

	@JvmStatic
	external fun playMainScene()

	@JvmStatic
	external fun setDebugMuteAudio(enabled: Boolean)

	@JvmStatic
	external fun resetTimeScale()

	@JvmStatic
	external fun setTimeScale(scale: Double)


	/**
	 * Returns [GameEmbedMode] stored in the editor settings.
	 *
	 * Must be called on the render thread.
	 */
	fun fetchGameEmbedMode(): GameEmbedMode {
		try {
			val gameEmbedModeValue = Integer.parseInt(OrblitLib.getEditorSetting(GameEmbedMode.SETTING_KEY))
			val gameEmbedMode = GameEmbedMode.fromNativeValue(gameEmbedModeValue) ?: GameEmbedMode.AUTO
			return gameEmbedMode
		} catch (e: Exception) {
			Log.w(TAG, "Unable to retrieve game embed mode", e)
			return GameEmbedMode.AUTO
		}
	}

	/**
	 * Update the 'game_embed_mode' editor setting.
	 *
	 * Must be called on the render thread.
	 */
	fun saveGameEmbedMode(gameEmbedMode: GameEmbedMode) {
		OrblitLib.setEditorSetting(GameEmbedMode.SETTING_KEY, gameEmbedMode.nativeValue)
	}
}
