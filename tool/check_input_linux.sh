#!/bin/bash
# Runs Orblit.Input's tests on Linux, against a gamepad the kernel invents.
#
# There is no controller attached to the machine this was developed on and no
# Steam Deck, so the read path is exercised the only honest way available:
# `/dev/uinput` is asked to create an event device with a pad's buttons, axes
# and ranges, and the backend opens and reads it exactly as it would hardware.
#
# Two things about containers are worth knowing, because both cost a while to
# work out and neither announces itself:
#
#   1. A container's /dev is a tmpfs. uinput creates the device, and it appears
#      under /sys/class/input straight away, but no node is ever made for it,
#      because devtmpfs is what makes nodes and it is not mounted there. So
#      this mounts a real devtmpfs at /devfs and points the tests at
#      /devfs/input through ORBLIT_INPUT_DEV. The failure without it is a pad
#      that sysfs can see and nothing can open.
#
#   2. Writing /dev/uinput needs more than the node being passed in with
#      --device: mounting devtmpfs needs CAP_SYS_ADMIN, so this runs
#      --privileged. On a real Linux host neither applies. The tests run
#      directly, needing only membership of a group that can write uinput.
#
# The image needs the .NET SDK and nothing else: Orblit.Input talks to libc
# directly, so there is no Filament and no GPU in the way. Name another image
# in ORBLIT_INPUT_IMAGE.
#
#   ./tool/check_input_linux.sh
#   ORBLIT_INPUT_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0 ./tool/check_input_linux.sh
set -uo pipefail
cd "$(dirname "$0")/.."

IMAGE="${ORBLIT_INPUT_IMAGE:-mcr.microsoft.com/dotnet/sdk:10.0}"

# A test project is an executable under xunit.v3, so it runs itself.
RUN="dotnet run --project dotnet/Orblit.Input.Tests"

# On a Linux host with uinput there is no reason to start a container at all.
if [ "$(uname -s)" = "Linux" ] && [ -w /dev/uinput ]; then
  echo "== Orblit.Input on this host =="
  $RUN
  exit $?
fi

if ! command -v docker > /dev/null; then
  echo "no docker, and this host cannot write /dev/uinput."
  echo "SKIPPED: the parsing and mapping tests still run under"
  echo "tool/check_dotnet.sh; only the end-to-end ones need a uinput device."
  exit 0
fi

echo "== Orblit.Input in $IMAGE =="
docker run --rm --privileged \
  -v "$PWD:/work" -w /work \
  -e ORBLIT_INPUT_DEV=/devfs/input \
  "$IMAGE" bash -c "
    set -e
    # The real device filesystem, because the container /dev is a tmpfs that
    # nothing will ever create a node in.
    mkdir -p /devfs && mount -t devtmpfs devtmpfs /devfs
    test -e /dev/uinput || { echo 'no /dev/uinput in this kernel'; exit 3; }
    $RUN
  "
status=$?

if [ "$status" -eq 3 ]; then
  echo
  echo "SKIPPED: this kernel has no uinput, so no virtual pad can be made."
  exit 0
fi
exit "$status"
