namespace Goro.Scratchpad.FileReading;

/// <summary>
/// Can TagLibSharp be made to give Id3v2 frames as recorded? FrameFactory.AddFrameCreator lets a
/// caller construct every frame itself. This records what such a creator is handed: the identifier
/// (converted or not), and the bytes (resynchronised, decompressed, or as stored).
/// </summary>
public static class TagLibHook
{
    sealed class RawFrame : TagLib.Id3v2.Frame
    {
        public RawFrame(TagLib.Id3v2.FrameHeader header, TagLib.ByteVector all) : base(header)
        {
            All = all.Data;
        }

        public byte[] All { get; }
        protected override void ParseFields(TagLib.ByteVector data, byte version) { }
        protected override TagLib.ByteVector RenderFields(byte version) => [];
    }

    public static void Run(string corpusDir)
    {
        var seen = new List<string>();
        TagLib.Id3v2.FrameFactory.AddFrameCreator((data, offset, header, version) =>
        {
            var size = (int)header.FrameSize;
            var headerSize = (int)TagLib.Id3v2.FrameHeader.Size(version);
            var stored = data.Mid(offset + headerSize, Math.Min(size, data.Count - offset - headerSize));
            var onDisk = System.Text.Encoding.ASCII.GetString(data.Mid(offset, version == 2 ? 3 : 4).Data);
            seen.Add($"v2.{version} on disk {onDisk} -> header id {header.FrameId}, flags {header.Flags}, {size} bytes: {Bin.Hex(stored.Data, 20)}");
            offset += headerSize + size;
            return new RawFrame(header, stored);
        });

        foreach (var file in new[] { "mp3/id3v23.mp3", "mp3/id3v22.mp3", "mp3/id3v23-unsync.mp3", "mp3/id3v24-frame-unsync.mp3", "mp3/id3v24-compressed.mp3", "mp3/id3v23-encrypted.mp3" })
        {
            seen.Clear();
            try
            {
                using var f = TagLib.File.Create(Path.Combine(corpusDir, file));
                var tag = (TagLib.Id3v2.Tag)f.GetTag(TagLib.TagTypes.Id3v2);
                Console.WriteLine($"== {file}: {tag.GetFrames().Count()} frames in the tag");
            }
            catch (Exception e)
            {
                Console.WriteLine($"== {file}: {e.GetType().Name}: {e.Message}");
            }

            seen.ForEach(s => Console.WriteLine("   " + s));
        }
    }
}
