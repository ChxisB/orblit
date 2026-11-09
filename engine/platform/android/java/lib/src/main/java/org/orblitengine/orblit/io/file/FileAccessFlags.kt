/**************************************************************************/
/*  FileAccessFlags.kt                                                    */

package org.orblitengine.orblit.io.file

/**
 * Android representation of Orblit native access flags.
 */
internal enum class FileAccessFlags(val nativeValue: Int) {
    /**
     * Opens the file for read operations.
     * The cursor is positioned at the beginning of the file.
     */
    READ(1),

    /**
     * Opens the file for write operations.
     * The file is created if it does not exist, and truncated if it does.
     */
    WRITE(2),

    /**
     * Opens the file for read and write operations.
     * Does not truncate the file. The cursor is positioned at the beginning of the file.
     */
    READ_WRITE(3),

    /**
     * Opens the file for read and write operations.
     * The file is created if it does not exist, and truncated if it does.
     * The cursor is positioned at the beginning of the file.
     */
    WRITE_READ(7);

    fun getMode(): String {
        return when (this) {
            READ -> "r"
            WRITE -> "w"
            READ_WRITE, WRITE_READ -> "rw"
        }
    }

    fun shouldTruncate(): Boolean {
        return when (this) {
            READ, READ_WRITE -> false
            WRITE, WRITE_READ -> true
        }
    }

    companion object {
        fun fromNativeModeFlags(modeFlag: Int): FileAccessFlags? {
            for (flag in entries) {
                if (flag.nativeValue == modeFlag) {
                    return flag
                }
            }
            return null
        }
    }
}
