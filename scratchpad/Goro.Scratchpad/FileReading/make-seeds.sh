#!/bin/sh
# Encodes the seed files of the audio fixture corpus: the only fixtures an encoder made. Every
# other fixture is derived from these by CorpusBuilder.cs, byte by byte, so that the tags and the
# damage in it are exactly what the builder says they are.
#
# Tool versions the committed seeds were made with: ffmpeg 9.0.2, LAME 4.0, flac 1.5.0,
# opus-tools 0.2 (libopus 1.6.1), vorbis-tools 1.4.3, speex 1.2.1. Re-running them, or the same
# tools at other versions, gives other bytes (oggenc picks a random serial number), so the
# committed seeds, not this script, are the reference.
#
# Usage: make-seeds.sh <work dir> <seeds dir>
set -eu
W=$1
S=$2
mkdir -p "$W" "$S"

# 3.3 s: a sweep, noise, then a tone, so that VBR encoders vary the bitrate across the file.
ffmpeg -v error -y -f lavfi \
  -i "aevalsrc='if(lt(t,1.1),0.5*sin(2*PI*(200+800*t)*t),if(lt(t,2.2),0.3*(random(0)*2-1),0.4*sin(2*PI*440*t)))':s=44100:d=3.3" \
  -ac 2 -c:a pcm_s16le "$W/src44s.wav"
ffmpeg -v error -y -i "$W/src44s.wav" -ac 1 "$W/src44m.wav"
ffmpeg -v error -y -i "$W/src44m.wav" -f s16le "$W/src44m.raw"
ffmpeg -v error -y -i "$W/src44s.wav" -ac 1 -ar 22050 "$W/src22m.wav"
ffmpeg -v error -y -i "$W/src44s.wav" -ac 1 -ar 8000 "$W/src8m.wav"
ffmpeg -v error -y -i "$W/src44s.wav" -ac 1 -ar 48000 "$W/src48m.wav"
# A second, shorter source for the second link of chained files and for joined MP3s.
ffmpeg -v error -y -f lavfi -i "sine=frequency=660:sample_rate=44100:duration=1.7" -ac 1 "$W/src44m-b.wav"

q() { "$@" >/dev/null 2>&1; }

# MP3, MPEG-1 Layer III, 44.1 kHz.
q lame --cbr -b 64 -m m "$W/src44m.wav" "$S/mp3-cbr.mp3"                 # Info + LAME tag
q lame --cbr -b 64 -m m -t "$W/src44m.wav" "$S/mp3-cbr-notag.mp3"        # no Info frame at all
q lame -V 2 -m m "$W/src44m.wav" "$S/mp3-vbr.mp3"                        # Xing + LAME tag
q lame -V 2 -m m -t "$W/src44m.wav" "$S/mp3-vbr-notag.mp3"               # VBR, no Xing
q lame -V 2 -m j "$W/src44s.wav" "$S/mp3-vbr-stereo.mp3"                 # Xing at the stereo offset
q lame --cbr -b 64 -m m -p "$W/src44m.wav" "$S/mp3-cbr-crc.mp3"          # CRC-protected frames
q lame --cbr -b 64 -m m "$W/src44m-b.wav" "$S/mp3-cbr-b.mp3"             # second half of a joined file
# MPEG-2 and MPEG-2.5: 576 samples per frame.
q lame -V 4 -m m "$W/src22m.wav" "$S/mp3-mpeg2-vbr.mp3"
q lame --cbr -b 16 -m m "$W/src8m.wav" "$S/mp3-mpeg25-cbr.mp3"
# Free format: no bitrate in the header, so frame length comes from the next sync word.
q lame --freeformat -b 80 -m m "$W/src44m.wav" "$S/mp3-freeformat.mp3" || echo "free format not written"

# Ogg Vorbis and Ogg Opus.
q oggenc -q 2 -o "$S/ogg-vorbis.ogg" "$W/src44m.wav"
q oggenc -q 2 -o "$W/vorbis-b.ogg" "$W/src44m-b.wav"
q opusenc --bitrate 32 "$W/src48m.wav" "$S/opus.opus"
q opusenc --bitrate 32 "$W/src44m.wav" "$S/opus-from44k.opus"            # input rate 44.1 kHz in OpusHead
q opusenc --bitrate 32 "$W/src44m-b.wav" "$W/opus-b.opus"
# Speex in Ogg, a codec Goro does not read, under the .ogg extension it often has (speexenc 1.2.1).
q speexenc "$W/src8m.wav" "$S/ogg-speex.ogg"
# Chained: logical streams one after another, which is what concatenation produces.
cat "$S/ogg-vorbis.ogg" "$W/vorbis-b.ogg" > "$S/ogg-chained-vorbis.ogg"
cat "$S/ogg-vorbis.ogg" "$W/opus-b.opus" > "$S/ogg-chained-vorbis-opus.ogg"
# Multiplexed: two logical streams interleaved.
ffmpeg -v error -y -i "$S/ogg-vorbis.ogg" -i "$W/vorbis-b.ogg" -map 0 -map 1 -c copy "$S/ogg-multiplexed.ogg"

# FLAC, from a quiet tonal source so that lossless fixtures stay small.
ffmpeg -v error -y -f lavfi \
  -i "aevalsrc='if(lt(t,1.6),0.01*sin(2*PI*(200+400*t)*t),0.01*sin(2*PI*440*t))':s=44100:d=3.3" \
  -ac 1 -c:a pcm_s16le "$W/flacsrc.wav"
ffmpeg -v error -y -i "$W/flacsrc.wav" -f s16le "$W/flacsrc.raw"
q flac -5 -f -o "$S/flac.flac" "$W/flacsrc.wav"
q flac -5 -f --no-seektable --no-padding -o "$S/flac-minimal.flac" "$W/flacsrc.wav"
q flac -5 -f --ogg -o "$S/flac-in-ogg.oga" "$W/flacsrc.wav"
# Piped: an encoder that cannot seek back leaves STREAMINFO's total samples and MD5 as zero.
flac -5 -s --force-raw-format --endian=little --sign=signed --channels=1 --bps=16 --sample-rate=44100 \
  -c - < "$W/flacsrc.raw" > "$S/flac-piped.flac" 2>/dev/null || true
