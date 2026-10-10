using Goro.Reading.Bytes;

namespace Goro.Reading.Flac;

/// <summary>
/// All of a FLAC file's playing-time logic (docs/implementation.md, "A FLAC file's playing time is
/// settled by the end of its audio"). STREAMINFO declares the stream's total samples, and the last
/// frame's header shows where the stream ends; the end of the audio says which to believe, or that
/// neither can be.
/// </summary>
public static class FlacDuration
{
    /// <summary>The least of the end of the file searched back over for the last frame.</summary>
    private const int TailSpan = 64 * 1024;

    /// <summary>
    /// The playing time of the stream whose first frame is at <paramref name="firstFrame"/>, its
    /// audio running to no further than <paramref name="end"/>, <c>F</c>. Junk found after the last
    /// frame is added to <paramref name="conditions"/>.
    /// </summary>
    public static DurationOutcome Decide(BoundedReader reader, StreamInfo? streamInfo, long firstFrame, long end, List<Condition> conditions)
    {
        if (!TryHeaderAt(reader, firstFrame, end, out var first))
        {
            return new DurationOutcome.Unusable(DurationProblem.LastFramesUnaccounted);
        }

        var rate = streamInfo is { SampleRate: > 0 } ? streamInfo.SampleRate : first.SampleRate;
        if (rate <= 0)
        {
            return new DurationOutcome.Unusable(DurationProblem.NoSampleRate);
        }

        var depth = first.BitDepth != 0 ? first.BitDepth : streamInfo?.BitDepth ?? 0;
        var nominal = first.BlockSize;
        var total = streamInfo?.TotalSamples ?? 0;
        if (FindLast(reader, streamInfo, firstFrame, end, first, depth) is not var (at, last, frameEnd))
        {
            return new DurationOutcome.Unusable(DurationProblem.LastFramesUnaccounted);
        }

        if (frameEnd is FlacFrameEnd.RunsPast)
        {
            // A frame cut off by the end of the audio: a truncation, and the timeline ends where it began.
            return Known(last.FirstSample(nominal), DurationBasis.Truncated);
        }

        var endSample = last.EndSample(nominal);
        var intactEnd = frameEnd is FlacFrameEnd.EndsAt { CrcPasses: true } ends ? at + ends.Length : (long?)null;
        if (total > 0 && endSample == total)
        {
            // The last header confirms STREAMINFO, whether or not its frame is intact: damage does not shorten a timeline.
            NoteJunk(intactEnd);
            return Known(total, DurationBasis.StreamInfoConfirmed);
        }

        if (intactEnd is not { } frameStop)
        {
            return new DurationOutcome.Unusable(DurationProblem.LastFramesUnaccounted);
        }

        if (!last.VariableBlocking && last.BlockSize < nominal)
        {
            NoteJunk(frameStop);
            return Known(endSample, DurationBasis.ShortFinalBlock);
        }

        return frameStop == end
            ? Known(endSample, DurationBasis.EndsAtTrailingTags)
            : new DurationOutcome.Unusable(DurationProblem.LastFramesUnaccounted);

        DurationOutcome Known(ulong samples, DurationBasis basis) => new DurationOutcome.Known((long)samples, rate, basis);

        void NoteJunk(long? stop)
        {
            if (stop is { } s && s < end)
            {
                conditions.Add(new BytesAfterAudio(Region.Between(s, end)));
            }
        }
    }

    /// <summary>
    /// The last frame header before <paramref name="end"/> that belongs to the stream, with where its
    /// body says it ends: searched back over the end of the audio, a header counting only when it
    /// has the first frame's blocking strategy, channels, sample rate and bit depth. What the tail
    /// window holds is searched first, which costs nothing; only where no frame starts there is the
    /// search taken further back.
    /// </summary>
    private static (long At, FlacFrameHeader Header, FlacFrameEnd End)? FindLast(
        BoundedReader reader, StreamInfo? streamInfo, long firstFrame, long end, FlacFrameHeader first, int depth)
    {
        var span = Math.Min(Math.Max(TailSpan, 2L * (streamInfo?.MaxFrameSize ?? 0)), Math.Max(TailSpan, reader.Policy.SearchBudget));
        var furthest = Math.Max(firstFrame, end - span);
        var inWindow = Math.Max(furthest, reader.Length - reader.Policy.TailWindow);
        return (inWindow < end ? Search(inWindow) : null) ?? (furthest < inWindow ? Search(furthest) : null);

        (long At, FlacFrameHeader Header, FlacFrameEnd End)? Search(long start)
        {
            if (end - start < 6 || !reader.TryRead(ReadPurpose.Search, start, end - start, out var tail))
            {
                return null;
            }

            var bytes = tail.Span;
            for (var i = bytes.Length - 6; i >= 0; i--)
            {
                if (bytes[i] == 0xFF
                    && FlacFrameHeader.TryParse(bytes[i..], out var header)
                    && header.VariableBlocking == first.VariableBlocking
                    && header.Channels == first.Channels
                    && header.SampleRate == first.SampleRate
                    && header.BitDepth == first.BitDepth)
                {
                    return (start + i, header, FlacFrameBody.FindEnd(bytes[i..], header, depth));
                }
            }

            return null;
        }
    }

    private static bool TryHeaderAt(BoundedReader reader, long at, long end, out FlacFrameHeader header)
    {
        header = default;
        var length = Math.Min(FlacFrameHeader.MaxLength, end - at);
        return length >= 6 && reader.TryRead(ReadPurpose.Sniff, at, length, out var bytes) && FlacFrameHeader.TryParse(bytes.Span, out header);
    }
}
