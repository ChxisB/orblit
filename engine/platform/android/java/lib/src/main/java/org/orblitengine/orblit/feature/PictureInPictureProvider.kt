/**************************************************************************/
/*  PictureInPictureProvider.kt                                           */

package org.orblitengine.orblit.feature

/**
 * Provides APIs to enable picture-in-picture.
 */
interface PictureInPictureProvider {

	fun enterPiPMode()

	fun isPiPModeSupported(): Boolean
}
