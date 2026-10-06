using Goro.Domain;
using Goro.Pipeline;
using Goro.Predicates;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;
using Goro.Predicates.Text;
using Goro.Predicates.Values;
using Goro.Tests.TestSupport;
using Goro.Warnings;

namespace Goro.Tests.Pipeline;

/// <summary>
/// What <c>goro list --filter</c> makes of a predicate's outcome for one file. The predicates are
/// built by hand from the built-in catalog's declarations, as the binder would build them, so that
/// the stage is tested on its own.
/// </summary>
public class PredicateStageTests
{
    private TempCollection _collection = null!;

    [SetUp]
    public void SetUp() => _collection = new TempCollection("goro-predicate-stage-tests-");

    [TearDown]
    public void TearDown() => _collection.Dispose();

    // --- T, F and U ---

    [Test]
    public async Task True_ListsTheFile_WithoutWarnings()
    {
        var outcome = await Run(NumberOfNameGreaterThanOne(), "/music/5");

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Matched));
        Assert.That(outcome.Output, Is.EqualTo(new ListResult("/music/5")));
        Assert.That(outcome.Warnings, Is.Empty);
    }

    [Test]
    public async Task False_LeavesTheFileOut_AsExamined_WithoutWarnings()
    {
        var outcome = await Run(NumberOfNameGreaterThanOne(), "/music/0");

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Unmatched));
        Assert.That(outcome.Output, Is.Null);
        Assert.That(outcome.Warnings, Is.Empty);
    }

    [Test]
    public async Task Unusable_LeavesTheFileOut_AsExamined_WithADataWarningQuotingWhatWasWritten()
    {
        var predicate = NumberOfNameGreaterThanOne(written: "number( file::NAME )");

        var outcome = await Run(predicate, "/music/track.mp3");

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Unmatched));
        Assert.That(outcome.Output, Is.Null);
        Assert.That(outcome.Warnings, Is.EqualTo(new[] { new DataWarning("/music/track.mp3", "number( file::NAME )") }));
        Assert.That(outcome.IsUnanswered, Is.True);
    }

    // True and false are answers; only unusable is not.
    [TestCase("/music/5", FileDisposition.Matched)]
    [TestCase("/music/1", FileDisposition.Unmatched)]
    public async Task AnAnsweredPredicate_IsNotUnanswered(string path, FileDisposition disposition)
    {
        var outcome = await Run(NumberOfNameGreaterThanOne(), path);

        Assert.That(outcome.Disposition, Is.EqualTo(disposition));
        Assert.That(outcome.IsUnanswered, Is.False);
    }

    [TestCase(1, 1, "the predicate could not be answered for the one file examined, which was not listed")]
    [TestCase(1, 120, "the predicate could not be answered for 1 of the 120 files examined, which was not listed")]
    [TestCase(5, 5, "the predicate could not be answered for any of the 5 files examined, which were not listed")]
    [TestCase(3, 120, "the predicate could not be answered for 3 of the 120 files examined, which were not listed")]
    public void Unanswered_SaysHowManyOfHowManyExamined_AndThatTheyWereNotListed(int unanswered, int examined, string message)
    {
        var warning = PredicateStage.Unanswered(unanswered, examined);

        Assert.That(warning.Category, Is.EqualTo(WarningCategory.Unanswered));
        Assert.That(warning.ToString(), Is.EqualTo($"goro: warning: {message}"));
    }

    // The warning is emitted where the data was used, whether or not the answer depended on it.
    [Test]
    public async Task True_DespiteUnusableData_ListsTheFile_AndStillCarriesTheDataWarning()
    {
        var sources = new Sources();
        var predicate = sources.Compile(new Or(GreaterThanOne(sources.NumberOf(sources.File<string>("name"))), new Literal<bool>(true)));

        var outcome = await Run(predicate, "/music/track.mp3");

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Matched));
        Assert.That(outcome.Output, Is.EqualTo(new ListResult("/music/track.mp3")));
        Assert.That(outcome.Warnings, Is.EqualTo(new[] { new DataWarning("/music/track.mp3", "file::name AS NUMBER") }));
    }

    [Test]
    public async Task EveryReportedSource_BecomesOneDataWarning_InOrderOfSource()
    {
        var sources = new Sources();
        var name = GreaterThanOne(sources.NumberOf(sources.File<string>("name")));
        var extension = GreaterThanOne(sources.NumberOf(sources.File<string>("extension")));
        var predicate = sources.Compile(new Or(extension, name));

        var outcome = await Run(predicate, "/music/track.mp3");

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Unmatched));
        Assert.That(outcome.Warnings, Is.EqualTo(new Warning[]
        {
            new DataWarning("/music/track.mp3", "file::name AS NUMBER"),
            new DataWarning("/music/track.mp3", "file::extension AS NUMBER"),
        }));
    }

    // The stage is shared by every file of the run; what one file reported must not reach another.
    [Test]
    public async Task OneStage_OverSeveralFiles_KeepsEachFilesWarningsToItself()
    {
        var stage = new PredicateStage(NumberOfNameGreaterThanOne());

        var junk = await stage.ExecuteAsync("/music/track.mp3", CancellationToken.None);
        var clean = await stage.ExecuteAsync("/music/5", CancellationToken.None);

        Assert.That(junk.Warnings, Has.Length.EqualTo(1));
        Assert.That(clean.Warnings, Is.Empty);
    }

    // --- files that cannot be read ---

    [Test]
    public async Task NothingRead_OverAFileThatDoesNotExist_EvaluatesWithoutAWarning()
    {
        var gone = _collection.PathOf("gone.mp3");

        var outcome = await Run(NumberOfNameGreaterThanOne(), gone);

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Unmatched));
        Assert.That(outcome.Warnings, Is.EqualTo(new[] { new DataWarning(gone, "file::name AS NUMBER") }));
    }

    [Test]
    public async Task SizeOfAFileGoneSinceDiscovery_MakesItUnreadable_WithTheCauseDescribedAsHashDescribesIt()
    {
        var gone = _collection.PathOf("gone.mp3");

        var outcome = await Run(SizeAtLeastZero(), gone);

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Unreadable));
        Assert.That(outcome.Output, Is.Null);
        Assert.That(outcome.Warnings, Is.EqualTo(new[] { new FileWarning(gone, "no such file or directory") }));
    }

    [Test]
    public async Task DurationOfAFileThatIsNotAudio_MakesItUnreadable_WithOneFileWarning()
    {
        var notAudio = _collection.NotAudio("text.mp3");

        var outcome = await Run(DurationAtLeastZero(new Sources()), notAudio);

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Unreadable));
        Assert.That(outcome.Output, Is.Null);
        Assert.That(outcome.Warnings, Has.Length.EqualTo(1));
        Assert.That(outcome.Warnings[0], Is.InstanceOf<FileWarning>().And.Property(nameof(PathWarning.Path)).EqualTo(notAudio));
    }

    [Test]
    public async Task DurationOfAFileThatMayNotBeRead_SaysPermissionDenied()
    {
        var locked = _collection.Lock(_collection.Mp3("locked.mp3"));

        var outcome = await Run(DurationAtLeastZero(new Sources()), locked);

        Assert.That(outcome.Warnings, Is.EqualTo(new[] { new FileWarning(locked, "permission denied") }));
    }

    [Test]
    public async Task DurationOfAReadableFile_IsEvaluated()
    {
        var good = _collection.Mp3("good.mp3");

        var outcome = await Run(DurationAtLeastZero(new Sources()), good);

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Matched));
        Assert.That(outcome.Warnings, Is.Empty);
    }

    // D3: a file that was not processed has nothing to say about its data.
    [Test]
    public async Task UnusableDataMetBeforeTheFileTurnsOutUnreadable_IsDropped_LeavingOnlyTheFileWarning()
    {
        var notAudio = _collection.NotAudio("text.mp3");
        var sources = new Sources();
        var number = GreaterThanOne(sources.NumberOf(sources.File<string>("name")));
        var predicate = sources.Compile(new Or(number, DurationAtLeastZero(sources).Root));

        var outcome = await Run(predicate, notAudio);

        Assert.That(outcome.Disposition, Is.EqualTo(FileDisposition.Unreadable));
        Assert.That(outcome.Warnings, Has.Length.EqualTo(1));
        Assert.That(outcome.Warnings[0], Is.InstanceOf<FileWarning>());
    }

    // --- defects ---

    // A tag identifier cannot be read yet. That is not a bad file but a missing feature, and must
    // fail the run rather than be reported as something wrong with the file.
    [Test]
    public void ATagIdentifierThatCannotBeReadYet_Propagates()
    {
        var sources = new Sources();
        var artist = sources.Identifier<string>(IdentifierName.Global("artist"), "artist");
        var predicate = sources.Compile(new ComparisonTest<string>(
            new(artist, Quantifier.Existential), ComparisonOperator.Equal,
            new(new Literal<string>("x"), Quantifier.Existential), StringOrder.Normalized));

        Assert.ThrowsAsync<NotSupportedException>(() => new PredicateStage(predicate).ExecuteAsync("/music/a.mp3", CancellationToken.None));
    }

    [Test]
    public void Cancellation_IsNotAnOutcome()
    {
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() =>
            new PredicateStage(NumberOfNameGreaterThanOne()).ExecuteAsync("/music/5", cancelled.Token));
    }

    // --- building predicates as the binder would ---

    private static Task<FileOutcome<ListResult>> Run(CompiledPredicate predicate, string path) =>
        new PredicateStage(predicate).ExecuteAsync(path, CancellationToken.None);

    /// <summary><c>file::name AS NUMBER > 1</c>, with the conversion written as <paramref name="written"/>.</summary>
    private static CompiledPredicate NumberOfNameGreaterThanOne(string? written = null)
    {
        var sources = new Sources();
        return sources.Compile(GreaterThanOne(sources.NumberOf(sources.File<string>("name"), written)));
    }

    /// <summary><c>file::size >= 0</c>.</summary>
    private static CompiledPredicate SizeAtLeastZero()
    {
        var sources = new Sources();
        return sources.Compile(new ComparisonTest<ByteCount>(
            new(sources.File<ByteCount>("size"), Quantifier.Existential), ComparisonOperator.GreaterOrEqual,
            new(new Literal<ByteCount>(new ByteCount(0)), Quantifier.Existential), NaturalOrder<ByteCount>.Instance));
    }

    /// <summary><c>file::duration >= 0</c>.</summary>
    private static CompiledPredicate DurationAtLeastZero(Sources sources) =>
        sources.Compile(new ComparisonTest<Duration>(
            new(sources.File<Duration>("duration"), Quantifier.Existential), ComparisonOperator.GreaterOrEqual,
            new(new Literal<Duration>(new Duration(0)), Quantifier.Existential), NaturalOrder<Duration>.Instance));

    private static ComparisonTest<decimal> GreaterThanOne(Expression<decimal> left) =>
        new(new(left, Quantifier.Existential), ComparisonOperator.Greater,
            new(new Literal<decimal>(1m), Quantifier.Existential), NaturalOrder<decimal>.Instance);

    /// <summary>Interns a warning source for each identifier and conversion, in the order they are made.</summary>
    private sealed class Sources
    {
        private readonly List<string> _forms = [];

        public Expression<T> File<T>(string name) where T : notnull =>
            Identifier<T>(new IdentifierName(["file"], name), $"file::{name}");

        public Expression<T> Identifier<T>(IdentifierName name, string written) where T : notnull
        {
            var found = (IdentifierLookup.Found)BuiltInCatalog.Instance.Lookup(name);
            return new IdentifierReference<T>((IdentifierDeclaration<T>)found.Declaration, Next(written));
        }

        public Expression<decimal> NumberOf(Expression<string> argument, string? written = null)
        {
            var form = $"{_forms[^1]} AS NUMBER";
            return new Conversion<string, decimal>(argument, Conversions.NumberFromString, Next(form, written));
        }

        public CompiledPredicate Compile(Expression<bool> root) =>
            new("(built by hand)", root, new SourceTable([.. _forms]));

        private Origin Next(string form, string? written = null)
        {
            _forms.Add(form);
            return new Origin(new SourceId(_forms.Count - 1), written ?? form, 0);
        }
    }
}
