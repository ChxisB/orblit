import 'package:orblit_remote/orblit_remote.dart';

/// Run by `server_test.dart` as a child process. `dart run` has asserts off, so
/// this is what a release build does with the server.
Future<void> main() async {
  try {
    final server = await RemoteServer.start(
      const RemoteHost(),
      announce: (_) {},
    );
    print('started');
    await server.close();
  } on StateError {
    print('refused');
  }
}
