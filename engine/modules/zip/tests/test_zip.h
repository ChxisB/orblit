/**************************************************************************/
/*  test_zip.h                                                            */

#pragma once

#include "../zip_packer.h"
#include "../zip_reader.h"

#include "tests/test_macros.h"
#include "tests/test_utils.h"

namespace TestZip {

void check_file_size(const String &p_path, int p_expected_size);

TEST_CASE("[ZIPPacker] default compression") {
	const String path = TestUtils::get_temp_path("compressed.zip");
	Ref<ZIPPacker> packer;
	packer.instantiate();
	Error open_result = packer->open(path, ZIPPacker::APPEND_CREATE);
	CHECK(open_result == OK);
	Error start_file_result = packer->start_file("demo.txt");
	CHECK(start_file_result == OK);
	String text = "hello world!";
	Error write_file_result = packer->write_file(text.to_utf8_buffer());
	CHECK(write_file_result == OK);
	Error close_file_result = packer->close_file();
	CHECK(close_file_result == OK);
	Error close_result = packer->close();
	CHECK(close_result == OK);
	check_file_size(path, 128);
}

TEST_CASE("[ZIPPacker] no compression") {
	const String path = TestUtils::get_temp_path("uncompressed.zip");
	Ref<ZIPPacker> packer;
	packer.instantiate();
	Error open_result = packer->open(path, ZIPPacker::APPEND_CREATE);
	CHECK(open_result == OK);
	packer->set_compression_level(ZIPPacker::COMPRESSION_NONE);
	Error start_file_result = packer->start_file("demo.txt");
	CHECK(start_file_result == OK);
	String text = "hello world!";
	Error write_file_result = packer->write_file(text.to_utf8_buffer());
	CHECK(write_file_result == OK);
	Error close_file_result = packer->close_file();
	CHECK(close_file_result == OK);
	Error close_result = packer->close();
	CHECK(close_result == OK);
	check_file_size(path, 131);
}

TEST_CASE("[ZIPReader] read files") {
	String test_data = String("engine/modules/zip/tests/data/").path_join("test.zip");
	Ref<ZIPReader> reader;
	reader.instantiate();
	Error open_result = reader->open(test_data);
	CHECK(open_result == OK);

	const String hello_path = "hello.txt";
	const String world_path = "world.txt";
	PackedStringArray expected_files;
	expected_files.push_back(hello_path);
	expected_files.push_back(world_path);
	CHECK(reader->get_files() == expected_files);

	const String expected_hello_text = "hello world!";
	const String expected_world_text = "game over!";
	PackedByteArray hello_bytes = reader->read_file(hello_path, false);
	PackedByteArray world_bytes = reader->read_file(world_path, true);
	CHECK(hello_bytes == expected_hello_text.to_utf8_buffer());
	CHECK(world_bytes == expected_world_text.to_utf8_buffer());

	CHECK(reader->get_compression_level(hello_path, true) == 6);
	CHECK(reader->get_compression_level(world_path, false) == 9);
}

} // namespace TestZip
