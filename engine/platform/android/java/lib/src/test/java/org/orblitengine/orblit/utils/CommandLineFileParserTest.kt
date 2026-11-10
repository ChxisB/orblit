/**************************************************************************/
/*  CommandLineFileParserTest.kt                                          */

package org.orblitengine.orblit.utils

import org.junit.Test
import org.junit.runner.RunWith
import org.junit.runners.Parameterized
import java.io.ByteArrayInputStream
import java.io.InputStream

// Orblit saves command line params in the `assets/_cl_` file on exporting an apk.  By default,
// without any other commands specified in `command_line/extra_args` in Export window, the content
// of that _cl_ file consists of only the `--xr_mode_regular` and `--use_immersive` flags.
// The `CL_` prefix here refers to that file
private val CL_DEFAULT_NO_EXTRA_ARGS = byteArrayOf(2, 0, 0, 0, 17, 0, 0, 0, 45, 45, 120, 114, 95, 109, 111, 100, 101, 95, 114, 101, 103, 117, 108, 97, 114, 15, 0, 0, 0, 45, 45, 117, 115, 101, 95, 105, 109, 109, 101, 114, 115, 105, 118, 101)
private val CL_ONE_EXTRA_ARG = byteArrayOf(3, 0, 0, 0, 15, 0, 0, 0, 45, 45, 117, 110, 105, 116, 95, 116, 101, 115, 116, 95, 97, 114, 103, 17, 0, 0, 0, 45, 45, 120, 114, 95, 109, 111, 100, 101, 95, 114, 101, 103, 117, 108, 97, 114, 15, 0, 0, 0, 45, 45, 117, 115, 101, 95, 105, 109, 109, 101, 114, 115, 105, 118, 101)
private val CL_TWO_EXTRA_ARGS = byteArrayOf(4, 0, 0, 0, 16, 0, 0, 0, 45, 45, 117, 110, 105, 116, 95, 116, 101, 115, 116, 95, 97, 114, 103, 49, 16, 0, 0, 0, 45, 45, 117, 110, 105, 116, 95, 116, 101, 115, 116, 95, 97, 114, 103, 50, 17, 0, 0, 0, 45, 45, 120, 114, 95, 109, 111, 100, 101, 95, 114, 101, 103, 117, 108, 97, 114, 15, 0, 0, 0, 45, 45, 117, 115, 101, 95, 105, 109, 109, 101, 114, 115, 105, 118, 101)
private val CL_EMPTY = byteArrayOf()
private val CL_HEADER_TOO_SHORT = byteArrayOf(0, 0, 0)
private val CL_INCOMPLETE_FIRST_ARG = byteArrayOf(2, 0, 0, 0, 17, 0, 0)
private val CL_LENGTH_TOO_LONG_IN_FIRST_ARG = byteArrayOf(2, 0, 0, 0, 17, 0, 0, 45, 45, 120, 114, 95, 109, 111, 100, 101, 95, 114, 101, 103, 117, 108, 97, 114, 15, 0, 0, 0, 45, 45, 117, 115, 101, 95, 105, 109, 109, 101, 114, 115, 105, 118, 101)
private val CL_MISMATCHED_ARG_LENGTH_AND_HEADER_ONE_ARG = byteArrayOf(2, 0, 0, 0, 10, 0, 0, 0, 45, 45, 120, 114)
private val CL_MISMATCHED_ARG_LENGTH_AND_HEADER_IN_FIRST_ARG = byteArrayOf(2, 0, 0, 0, 17, 0, 0, 0, 45, 45, 120, 114, 95, 109, 111, 100, 101, 95, 114, 101, 103, 117, 108, 97, 15, 0, 0, 0, 45, 45, 117, 115, 101, 95, 105, 109, 109, 101, 114, 115, 105, 118, 101)

@RunWith(Parameterized::class)
class CommandLineFileParserTest(
	private val inputStreamArg: InputStream,
	private val expectedResult: List<String>,
) {

	companion object {
		@JvmStatic
		@Parameterized.Parameters
		fun data() = listOf(
			arrayOf(ByteArrayInputStream(CL_EMPTY), listOf<String>()),
			arrayOf(ByteArrayInputStream(CL_HEADER_TOO_SHORT), listOf<String>()),

			arrayOf(ByteArrayInputStream(CL_DEFAULT_NO_EXTRA_ARGS), listOf(
				"--xr_mode_regular",
				"--use_immersive",
			)),

			arrayOf(ByteArrayInputStream(CL_ONE_EXTRA_ARG), listOf(
				"--unit_test_arg",
				"--xr_mode_regular",
				"--use_immersive",
			)),

			arrayOf(ByteArrayInputStream(CL_TWO_EXTRA_ARGS), listOf(
				"--unit_test_arg1",
				"--unit_test_arg2",
				"--xr_mode_regular",
				"--use_immersive",
			)),

			arrayOf(ByteArrayInputStream(CL_INCOMPLETE_FIRST_ARG), listOf<String>()),
			arrayOf(ByteArrayInputStream(CL_LENGTH_TOO_LONG_IN_FIRST_ARG), listOf<String>()),
			arrayOf(ByteArrayInputStream(CL_MISMATCHED_ARG_LENGTH_AND_HEADER_ONE_ARG), listOf<String>()),
			arrayOf(ByteArrayInputStream(CL_MISMATCHED_ARG_LENGTH_AND_HEADER_IN_FIRST_ARG), listOf<String>()),
		)
	}

	@Test
	fun `Given inputStream, When parsing command line, Then a correct list is returned`() {
		// given
		val inputStream = inputStreamArg

		// when
		val result = CommandLineFileParser.parseCommandLine(inputStream)

		// then
		assert(result == expectedResult) { "Expected: $expectedResult Actual: $result" }
	}
}
