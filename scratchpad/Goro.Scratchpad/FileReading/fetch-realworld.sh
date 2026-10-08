#!/bin/sh
# Fetches other projects' audio test data, pinned to the commits the file-reading survey used, into
# a directory outside the repository. Their licences (GPL-2.0, LGPL-2.1/MPL-1.1, LGPL-2.1, MIT)
# have not been weighed for committing any of it, so it stays outside.
#
# Usage: fetch-realworld.sh <dir>, then:
#   dotnet run -c Release --project scratchpad/Goro.Scratchpad -- realworld <dir> <dir>/proto.jsonl
set -eu
D=$1
mkdir -p "$D"
fetch() { # repo, directory, path inside it, commit
  git clone -q --filter=blob:none --sparse --no-checkout "$1" "$D/$2"
  git -C "$D/$2" sparse-checkout set "$3"
  git -C "$D/$2" checkout -q "$4"
}
fetch https://github.com/quodlibet/mutagen mutagen tests/data ada28b2
fetch https://github.com/taglib/taglib taglib tests/data 961dd69
fetch https://github.com/mono/taglib-sharp taglib-sharp tests/TaglibSharp.Tests/samples da41dc3
fetch https://github.com/Borewit/music-metadata music-metadata test/samples 9b71259
