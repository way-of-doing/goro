using System.Collections;
using System.Diagnostics;
using System.Reflection;

namespace Goro.Scratchpad.FileReading;

/// <summary>A field as a candidate reader presents it. Tag "*" means the reader merges tags into one view.</summary>
public sealed record Observed(string Tag, string Key, string? Description, string[]? Values, byte[]? Bytes);

public sealed record CandidateResult(string Reader, double? Seconds, string? Error, List<Observed> Fields, List<string> Notes, double Milliseconds);

/// <summary>Adapters that run each candidate library over a file and report what it saw, in one shape.</summary>
public static class Candidates
{
    public static IEnumerable<(string Name, Func<string, CandidateResult> Run)> All(string extension)
    {
        yield return ("goro-prototype", Prototype);
        yield return ("TagLibSharp 2.3.0", TagLibSharp);
        yield return ("ATL 7.18.0", p => Atl(p, exact: false));
        if (extension == ".mp3")
        {
            yield return ("ATL exact-duration", p => Atl(p, exact: true));
            yield return ("NLayer 3.0.0", NLayerDuration);
        }

        yield return ("TagLibSharp2 0.6.0", TagLibSharp2);
        if (extension is ".ogg")
        {
            yield return ("NVorbis 0.10.5", NVorbisDuration);
        }

        if (extension is ".opus")
        {
            yield return ("Concentus.Oggfile 1.0.7", ConcentusDuration);
        }
    }

    static CandidateResult Timed(string reader, Func<(double?, List<Observed>, List<string>)> run)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var (seconds, fields, notes) = run();
            return new CandidateResult(reader, seconds, null, fields, notes, sw.Elapsed.TotalMilliseconds);
        }
        catch (Exception e)
        {
            return new CandidateResult(reader, null, $"{e.GetType().Name}: {e.Message.Split('\n')[0]}", [], [], sw.Elapsed.TotalMilliseconds);
        }
    }

    // ---------------------------------------------------------------- our own prototype

    static CandidateResult Prototype(string path) => Timed("goro-prototype", () =>
    {
        var r = FormatReaders.Read(path);
        var fields = r.Tags.Where(t => t.IsSource)
            .SelectMany(t => t.Fields.Where(f => f.Quirk != "illegal frame identifier, stepped over")
                .Select(f => new Observed(Family(t.Tag), f.Key, f.Description, f.Values, f.Content ?? f.Stored))).ToList();
        var best = r.Best("policy");
        var notes = r.Problems.Concat(r.Tags.Where(t => t.StructureProblem is not null).Select(t => $"{t.Tag}: {t.StructureProblem}"))
            .Concat(r.Tags.SelectMany(t => t.Fields.Where(f => f.Problem is not null).Select(f => $"{t.Tag} {f.Key}: {f.Problem}")))
            .Concat(r.Tags.SelectMany(t => t.Fields.Where(f => f.Quirk is not null).Select(f => $"{t.Tag} {f.Key}: quirk: {f.Quirk}")))
            .Append("durations: " + string.Join(", ", r.Durations)).Distinct().ToList();
        return (best is { Rate: > 0 } ? best.Seconds : null, fields, notes);
    });

    public static string Family(string tag) =>
        tag.StartsWith("id3v2") ? "id3v2" : tag.StartsWith("ape") ? "ape" : tag.StartsWith("id3v1") ? "id3v1" : tag;

    // ---------------------------------------------------------------- TagLibSharp

    static CandidateResult TagLibSharp(string path) => Timed("TagLibSharp", () =>
    {
        using var f = TagLib.File.Create(path);
        var fields = new List<Observed>();
        var notes = new List<string>();
        if (f.PossiblyCorrupt)
        {
            notes.Add("PossiblyCorrupt: " + string.Join("; ", f.CorruptionReasons));
        }

        if (f.TagTypesOnDisk.HasFlag(TagLib.TagTypes.Id3v2) && f.GetTag(TagLib.TagTypes.Id3v2, false) is TagLib.Id3v2.Tag id3)
        {
            notes.Add($"id3v2 version {id3.Version}");
            foreach (var frame in id3.GetFrames())
            {
                var id = frame.FrameId.ToString();
                fields.Add(frame switch
                {
                    TagLib.Id3v2.UserTextInformationFrame u => new Observed("id3v2", id, u.Description, u.Text, null),
                    TagLib.Id3v2.TextInformationFrame t => new Observed("id3v2", id, null, t.Text, null),
                    TagLib.Id3v2.CommentsFrame c => new Observed("id3v2", id, c.Description, [c.Text], null),
                    TagLib.Id3v2.UnsynchronisedLyricsFrame l => new Observed("id3v2", id, l.Description, [l.Text], null),
                    TagLib.Id3v2.UserUrlLinkFrame uu => new Observed("id3v2", id, uu.Description, uu.Text, null),
                    TagLib.Id3v2.UrlLinkFrame w => new Observed("id3v2", id, null, w.Text, null),
                    TagLib.Id3v2.AttachmentFrame a => new Observed("id3v2", id, a.Description, null, a.Data.Data),
                    TagLib.Id3v2.PrivateFrame p => new Observed("id3v2", id, p.Owner, null, p.PrivateData.Data),
                    TagLib.Id3v2.UnknownFrame k => new Observed("id3v2", id, null, null, k.Data.Data),
                    _ => new Observed("id3v2", id, null, null, null),
                });
            }
        }

        if (f.TagTypesOnDisk.HasFlag(TagLib.TagTypes.Ape) && f.GetTag(TagLib.TagTypes.Ape, false) is TagLib.Ape.Tag ape)
        {
            foreach (string key in ape)
            {
                var item = ape.GetItem(key);
                fields.Add(new Observed("ape", item.Key, null, item.Type == TagLib.Ape.ItemType.Binary ? null : item.ToStringArray(), item.Value?.Data));
            }
        }

        if (f.TagTypesOnDisk.HasFlag(TagLib.TagTypes.Xiph) && f.GetTag(TagLib.TagTypes.Xiph, false) is TagLib.Ogg.XiphComment xiph)
        {
            foreach (string key in xiph)
            {
                foreach (var v in xiph.GetField(key))
                {
                    fields.Add(new Observed("vorbis", key, null, [v], null));
                }
            }
        }

        if (f.TagTypesOnDisk.HasFlag(TagLib.TagTypes.Id3v1) && f.GetTag(TagLib.TagTypes.Id3v1, false) is TagLib.Id3v1.Tag v1)
        {
            fields.Add(new Observed("id3v1", "title", null, [v1.Title], null));
            fields.Add(new Observed("id3v1", "artist", null, v1.Performers, null));
            fields.Add(new Observed("id3v1", "genre", null, v1.Genres, null));
            fields.Add(new Observed("id3v1", "track", null, [v1.Track.ToString()], null));
        }

        notes.Add($"tag types: {f.TagTypesOnDisk}; invariant range [{f.InvariantStartPosition}, {f.InvariantEndPosition})");
        return (f.Properties?.Duration.TotalSeconds, fields, notes);
    });

    // ---------------------------------------------------------------- ATL

    static readonly object AtlLock = new();

    static CandidateResult Atl(string path, bool exact) => Timed(exact ? "ATL exact" : "ATL", () =>
    {
        lock (AtlLock)
        {
            ATL.Settings.MP3_parseExactDuration = exact;
            ATL.Settings.UseFileNameWhenNoTitle = false;
            ATL.Settings.OutputStacktracesToConsole = false;
            var messages = new List<string>();
            var t = new ATL.Track(path);
            var fields = new List<Observed>();
            void Add(string key, string? v)
            {
                if (!string.IsNullOrEmpty(v))
                {
                    fields.Add(new Observed("*", key, null, v.Split(ATL.Settings.DisplayValueSeparator), null));
                }
            }

            Add("Title", t.Title);
            Add("Artist", t.Artist);
            Add("Album", t.Album);
            Add("Genre", t.Genre);
            Add("Comment", t.Comment);
            Add("AlbumArtist", t.AlbumArtist);
            Add("TrackNumber", t.TrackNumberStr);
            Add("Year", t.Year?.ToString());
            foreach (var (k, v) in t.AdditionalFields)
            {
                Add(k, v);
            }

            foreach (var p in t.EmbeddedPictures)
            {
                fields.Add(new Observed("*", "picture", p.Description, null, p.PictureData));
            }

            var notes = new List<string> { $"formats: {string.Join(",", t.MetadataFormats.Select(m => m.Name))}; audio {t.AudioFormat.Name}" };
            return (t.DurationMs / 1000.0, fields, notes);
        }
    });

    // ---------------------------------------------------------------- TagLibSharp2

    static CandidateResult TagLibSharp2(string path) => Timed("TagLibSharp2", () =>
    {
        var r = global::TagLibSharp2.Core.MediaFile.Read(path);
        if (!r.IsSuccess)
        {
            throw new InvalidDataException("read failed: " + r.Error);
        }

        var fields = new List<Observed>();
        var notes = new List<string> { $"format {r.Format}" };
        double? seconds = null;
        switch (r.File)
        {
            case global::TagLibSharp2.Mpeg.Mp3File mp3:
                seconds = mp3.Properties?.Duration.TotalSeconds;
                if (mp3.Id3v2Tag is { } id3)
                {
                    notes.Add($"id3v2 version {id3.Version}");
                    foreach (var list in new IEnumerable?[] { id3.Frames, id3.UserTextFrames, id3.Comments, id3.LyricsFrames, id3.UrlFrames, id3.UserUrlFrames, id3.PictureFrames, id3.PrivateFrames, id3.PopularimeterFrames, id3.InvolvedPeopleFrames })
                    {
                        foreach (var frame in list ?? Array.Empty<object>())
                        {
                            fields.Add(Reflect("id3v2", frame!));
                        }
                    }
                }

                if (mp3.Id3v1Tag is { } v1)
                {
                    fields.Add(new Observed("id3v1", "title", null, [v1.Title ?? ""], null));
                    fields.Add(new Observed("id3v1", "artist", null, [v1.Artist ?? ""], null));
                }

                break;
            case global::TagLibSharp2.Xiph.FlacFile flac:
                seconds = flac.Properties.Duration.TotalSeconds;
                AddXiph(flac.VorbisComment, fields);
                break;
            case global::TagLibSharp2.Ogg.OggVorbisFile ov:
                seconds = ov.Properties.Duration.TotalSeconds;
                AddXiph(ov.VorbisComment, fields);
                break;
            case global::TagLibSharp2.Ogg.OggOpusFile oo:
                seconds = oo.Properties.Duration.TotalSeconds;
                AddXiph(oo.VorbisComment, fields);
                break;
            default:
                notes.Add($"file type {r.File?.GetType().Name}");
                break;
        }

        return (seconds, fields, notes);
    });

    static void AddXiph(global::TagLibSharp2.Xiph.VorbisComment? c, List<Observed> fields)
    {
        if (c is null)
        {
            return;
        }

        foreach (var field in c.Fields)
        {
            fields.Add(new Observed("vorbis", field.Name, null, [field.Value], null));
        }
    }

    /// <summary>Reads a TagLibSharp2 frame by its property names, so that one adapter covers every frame class.</summary>
    static Observed Reflect(string tag, object frame)
    {
        string? Get(params string[] names) => names.Select(n => frame.GetType().GetProperty(n, BindingFlags.Public | BindingFlags.Instance)?.GetValue(frame))
            .FirstOrDefault(v => v is not null) switch
        {
            null => null,
            string s => s,
            IEnumerable<string> e => string.Join("\0", e),
            var o => o.ToString(),
        };

        var id = Get("Id", "FrameId") ?? frame.GetType().Name switch
        {
            "UserTextFrame" => "TXXX",
            "CommentFrame" => "COMM",
            "LyricsFrame" => "USLT",
            "UserUrlFrame" => "WXXX",
            "PictureFrame" => "APIC",
            "PrivateFrame" => "PRIV",
            "PopularimeterFrame" => "POPM",
            var other => other,
        };
        var bytesObj = new[] { "PictureData", "Data", "RawData" }.Select(n => frame.GetType().GetProperty(n)?.GetValue(frame)).FirstOrDefault(v => v is not null);
        byte[]? bytes = bytesObj switch
        {
            byte[] b => b,
            ReadOnlyMemory<byte> m => m.ToArray(),
            global::TagLibSharp2.Core.BinaryData d => d.ToArray(),
            _ => null,
        };
        var text = Get("Text", "Value", "Url");
        return new Observed(tag, id, Get("Description"), text?.Split('\0'), bytes);
    }

    // ---------------------------------------------------------------- decoders, for duration only

    static CandidateResult NLayerDuration(string path) => Timed("NLayer", () =>
    {
        using var m = new NLayer.MpegFile(path);
        return (m.Duration.TotalSeconds, [], [$"length {m.Length} samples"]);
    });

    static CandidateResult NVorbisDuration(string path) => Timed("NVorbis", () =>
    {
        using var v = new NVorbis.VorbisReader(path);
        return (v.TotalTime.TotalSeconds, [], [$"streams {v.StreamCount}, total samples {v.TotalSamples}"]);
    });

    static CandidateResult ConcentusDuration(string path) => Timed("Concentus", () =>
    {
        using var s = File.OpenRead(path);
        var decoder = Concentus.OpusCodecFactory.CreateDecoder(48000, 1);
        var r = new Concentus.Oggfile.OpusOggReadStream(decoder, s);
        return (r.TotalTime.TotalSeconds, [], [$"granules {r.GranuleCount}{(r.LastError is null ? "" : ", error " + r.LastError)}"]);
    });
}
