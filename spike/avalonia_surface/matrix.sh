#!/bin/bash
# The full spike matrix. SPIKE_DIRTY keeps Avalonia ticking at the display
# rate on every path, so the paths are compared like for like. Throwaway.
cd "$(dirname "$0")"
export SPIKE_DIRTY=1
for p in A B C; do
  ./run.sh $p 1920x1080 1 --frames 300
  ./run.sh $p 3840x2160 1 --frames 300
  ./run.sh $p 1600x900 2 --frames 300
  ./run.sh $p 1920x1080 1 --frames 300 --resize --popup --shot ../results/shots/path$p-popup.png
done
./run.sh C 1920x1080 1 --frames 3000 --warmup 60
