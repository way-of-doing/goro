using Goro.Warnings;

namespace Goro.Tests.Warnings;

public class WarningSinkTests
{
    private static readonly DataWarning Data = new("/music/a.mp3", "NUMBER(file::name)");
    private static readonly FileWarning File = new("/music/b.mp3", "permission denied");

    private static string[] Lines(StringWriter writer) =>
        writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    [Test]
    public void Emit_WritesEachWarningAsOneLine_AndRecordsItsCategory()
    {
        var writer = new StringWriter();
        var sink = new WarningSink(writer, WarningCategories.None);

        sink.Emit([Data]);
        sink.Emit([File]);

        Assert.That(Lines(writer), Is.EqualTo(new[]
        {
            "goro: warning: /music/a.mp3: cannot interpret the data of NUMBER(file::name)",
            "goro: warning: /music/b.mp3: cannot be read: permission denied",
        }));
        Assert.That(sink.Produced, Is.EquivalentTo(new[] { WarningCategory.Data, WarningCategory.File }));
    }

    [Test]
    public void Produced_NothingEmitted_IsEmpty()
    {
        var sink = new WarningSink(new StringWriter(), WarningCategories.None);

        Assert.That(sink.Produced, Is.Empty);
    }

    [Test]
    public void Emit_SuppressedCategory_IsNeitherWrittenNorRecorded()
    {
        var writer = new StringWriter();
        var sink = new WarningSink(writer, new HashSet<WarningCategory> { WarningCategory.Data });

        sink.Emit([Data, File]);

        Assert.That(Lines(writer), Is.EqualTo(new[] { File.ToString() }));
        Assert.That(sink.Produced, Is.EquivalentTo(new[] { WarningCategory.File }));
    }

    [Test]
    public void Emit_EveryCategorySuppressed_LeavesNoTraceAtAll()
    {
        var writer = new StringWriter();
        var sink = new WarningSink(writer, WarningCategories.All);

        sink.Emit([Data, File]);

        Assert.That(writer.ToString(), Is.Empty);
        Assert.That(sink.Produced, Is.Empty);
    }

    // Many files finish at once; a file's warnings must still reach standard error together.
    [Test]
    public void Emit_ConcurrentBatches_StayContiguous()
    {
        var writer = new StringWriter();
        var sink = new WarningSink(writer, WarningCategories.None);
        const int files = 200;
        const int perFile = 5;

        Parallel.For(0, files, i =>
            sink.Emit([.. Enumerable.Range(0, perFile).Select(j => new DataWarning($"/f{i}.mp3", $"x{j}"))]));

        var lines = Lines(writer);
        Assert.That(lines, Has.Length.EqualTo(files * perFile));
        var paths = lines.Select(l => l.Split(": ")[1]).ToArray();
        for (var start = 0; start < paths.Length; start += perFile)
        {
            Assert.That(paths.Skip(start).Take(perFile).Distinct().Count(), Is.EqualTo(1), $"batch at line {start} was interleaved");
        }
    }
}
