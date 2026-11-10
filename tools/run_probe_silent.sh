#!/bin/bash
# Runs a built-player probe with no usable audio device, so it cannot make noise on the developer's speakers.
# Usage: tools/run_probe_silent.sh <player-dir> <probe-flag> <logfile>   e.g. Builds/Linux --smoke-test /tmp/smoke.log
# (PulseAudio is pointed at a socket that does not exist and ALSA at an empty config, so FMOD falls back to no output.)
dir="$1"; flag="$2"; log="$3"
cd "$dir" || exit 2
env PULSE_SERVER=unix:/nonexistent/none ALSA_CONFIG_PATH=/dev/null ALSA_CONFIG_DIR=/dev/null \
  SDL_AUDIODRIVER=dummy AUDIODEV=null ./SheltreSim.x86_64 -batchmode -nographics "$flag" -logFile "$log" > /dev/null 2>&1
