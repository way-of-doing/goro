namespace Goro.Scratchpad.FileReading;

/// <summary>
/// Where the candidate edges of an MP3's audio fall, for every MP3 in the corpus: TagLibSharp's
/// invariant range (what goro hash hashes today), the end of the leading tags and the start of the
/// trailing tags as the prototype finds them, the first confirmed frame, and the end of the last
/// whole frame.
/// </summary>
public static class Boundaries
{
    public static void Run(string corpusDir)
    {
        Console.WriteLine($"{"file",-42} {"size",6}  {"taglib",-13}  {"tags end",8} {"1st frame",9} {"last end",8} {"trail tag",9}");
        foreach (var path in Directory.EnumerateFiles(corpusDir, "*.mp3", SearchOption.AllDirectories).Order())
        {
            var f = File.ReadAllBytes(path);
            string taglib;
            try
            {
                using var t = TagLib.File.Create(path);
                taglib = $"[{t.InvariantStartPosition},{t.InvariantEndPosition})";
            }
            catch (Exception e)
            {
                taglib = e.GetType().Name[..Math.Min(13, e.GetType().Name.Length)];
            }

            var r = FormatReaders.Mp3(f);
            var first = -1L;
            for (var i = r.AudioStart; i + 4 <= r.AudioEnd; i++)
            {
                if (f[i] == 0xFF && MpegHeader.TryParse(f, (int)i, out var h) && h.FrameLength > 0 &&
                    MpegHeader.TryParse(f, (int)(i + h.FrameLength), out var n) && n.Matches(h))
                {
                    first = i;
                    break;
                }
            }

            // Walk whole frames from the first; the last whole frame ends where the walk stops.
            var lastEnd = first;
            if (first >= 0)
            {
                MpegHeader.TryParse(f, (int)first, out var s);
                var at = first;
                while (MpegHeader.TryParse(f, (int)at, out var h) && h.Matches(s) && h.FrameLength > 0 && at + h.FrameLength <= r.AudioEnd)
                {
                    at += h.FrameLength;
                    lastEnd = at;
                }
            }

            Console.WriteLine($"{Path.GetRelativePath(corpusDir, path),-42} {f.Length,6}  {taglib,-13}  {r.AudioStart,8} {first,9} {lastEnd,8} {r.AudioEnd,9}");
        }
    }
}
