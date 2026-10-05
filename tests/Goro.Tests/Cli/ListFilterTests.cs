using System.Text.Json;
using Goro.Discovery;
using Goro.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace Goro.Tests.Cli;

/// <summary>
/// <c>goro list --filter</c> end to end: which files it keeps, and that a predicate with errors in
/// it is rejected before anything is processed. The rows of docs/testing.md about unreadable files
/// and warning suppression under a filter are in <see cref="UnreadableFileTests"/> and
/// <see cref="WarningSuppressionTests"/>.
/// </summary>
/// <remarks>
/// A predicate that does not parse never reaches the binder, so the syntax errors here run today;
/// everything else waits for the binder.
/// </remarks>
public class ListFilterTests
{
    private TempCollection _collection = null!;

    [SetUp]
    public void SetUp() => _collection = new TempCollection("goro-cli-filter-tests-");

    [TearDown]
    public void TearDown() => _collection.Dispose();

    private static string[] Lines(string text) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Runs Goro with discovery observed, so that a test can tell whether anything was processed.</summary>
    private static async Task<(int ExitCode, string StdOut, string StdErr, RecordingDiscovery Discovery)> RunObservedAsync(params string[] args)
    {
        var discovery = new RecordingDiscovery(new FileDiscoveryService());
        var app = GoroAppFactory.Create(services => services.AddSingleton<IFileDiscoveryService>(discovery));
        var (exitCode, stdOut, stdErr) = await app.RunCapturedAsync(args);
        return (exitCode, stdOut, stdErr, discovery);
    }

    private static void AssertRejectedBeforeAnythingWasProcessed(int exitCode, string stdOut, RecordingDiscovery discovery)
    {
        Assert.That(exitCode, Is.EqualTo(2), "exit code");
        Assert.That(stdOut, Is.Empty, "standard output");
        Assert.That(discovery.Resolved, Is.False, "the pathspecs were resolved");
        Assert.That(discovery.Discovered, Is.False, "files were discovered");
    }

    // --- errors in the predicate: these need only the parser ---

    public static IEnumerable<TestCaseData> SyntaxErrors()
    {
        yield return new TestCaseData(
            @"file::name = ""a.mp3""",
            new[] { "goro: error: We compare with `==`, goro!", @"  file::name = ""a.mp3""", "             ^", @"  try: file::name == ""a.mp3""" })
            .SetName("A borrowed operator is answered with the one meant");
        yield return new TestCaseData(
            @"file::name =~ ""^a""",
            new[] { @"  file::name =~ ""^a""", @"  try: file::name =~ r""^a""" })
            .SetName("A quoted pattern is answered with its raw form");
        yield return new TestCaseData(
            @"file::name ~= ""a""",
            new[] { @"  try: file::name =~ r""a""", @"  try: file::name != ""a""" })
            .SetName("~= is answered with both operators it may have meant");
        yield return new TestCaseData(
            "file::size > 1 > 0",
            new[] { "  file::size > 1 > 0", "                 ^", "  try: file::size > 1 AND 1 > 0" })
            .SetName("A chained comparison is answered with AND");
    }

    [TestCaseSource(nameof(SyntaxErrors))]
    public async Task ASyntaxError_IsRejectedWith2BeforeAnythingIsProcessed_AndExplainedOnStandardError(string predicate, string[] expectedLines)
    {
        _collection.Mp3("a.mp3");

        var (exitCode, stdOut, stdErr, discovery) = await RunObservedAsync("list", $"--filter={predicate}", _collection.Root);

        AssertRejectedBeforeAnythingWasProcessed(exitCode, stdOut, discovery);
        Assert.That(Lines(stdErr), Is.SupersetOf(expectedLines), stdErr);
        Assert.That(Lines(stdErr)[0], Does.StartWith("goro: error: "));
    }

    [Test]
    public async Task AnEmptyPredicate_IsRejectedWith2BeforeAnythingIsProcessed()
    {
        var (exitCode, stdOut, stdErr, discovery) = await RunObservedAsync("list", "--filter", "", _collection.Root);

        AssertRejectedBeforeAnythingWasProcessed(exitCode, stdOut, discovery);
        Assert.That(stdErr.Trim(), Is.EqualTo("goro: error: Give the filter something to check, goro!"));
    }

    // The predicate is checked first, needing nothing but its own text, and the first rejection
    // ends the run.
    [Test]
    public async Task APredicateErrorAndAMissingPathspec_ReportsThePredicate()
    {
        var missing = _collection.PathOf("missing.mp3");

        var (exitCode, stdOut, stdErr, discovery) = await RunObservedAsync("list", @"--filter=file::name = ""a.mp3""", missing);

        AssertRejectedBeforeAnythingWasProcessed(exitCode, stdOut, discovery);
        Assert.That(stdErr, Does.Contain("We compare with `==`, goro!"));
        Assert.That(stdErr, Does.Not.Contain(missing));
    }

    // Spectre would read a separate value beginning with a dash as more options, and bind "-1".
    [Test]
    public async Task APredicateGivenAsASeparateArgumentBeginningWithADash_IsTakenWhole()
    {
        var (exitCode, _, stdErr, _) = await RunObservedAsync("list", "--filter", "-1 = 2", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(2));
        Assert.That(Lines(stdErr), Does.Contain("  -1 = 2"));
    }

    [Test]
    public async Task AFilterOptionWithNoValue_IsACommandLineError()
    {
        var (exitCode, stdOut, _, discovery) = await RunObservedAsync("list", _collection.Root, "--filter");

        AssertRejectedBeforeAnythingWasProcessed(exitCode, stdOut, discovery);
    }

    // --- errors in the predicate that the binder finds ---

    [Test]
    public async Task AnUnknownIdentifier_IsRejectedWith2BeforeAnythingIsProcessed_SuggestingTheClosest()
    {
        _collection.Mp3("a.mp3");

        var (exitCode, stdOut, stdErr, discovery) = await RunObservedAsync("list", @"--filter=file::nmae == ""a.mp3""", _collection.Root);

        AssertRejectedBeforeAnythingWasProcessed(exitCode, stdOut, discovery);
        Assert.That(Lines(stdErr), Does.Contain(@"  file::nmae == ""a.mp3""").And.Contain("  ^^^^^^^^^^"));
        Assert.That(Lines(stdErr), Does.Contain(@"  try: file::name == ""a.mp3"""));
    }

    [Test]
    public async Task NotEqualOnAnOperandThatIsNotDefinite_IsRejected_OfferingBothRewrites()
    {
        _collection.Mp3("a.mp3");

        var (exitCode, stdOut, stdErr, discovery) = await RunObservedAsync("list", @"--filter=genre != ""metal""", _collection.Root);

        AssertRejectedBeforeAnythingWasProcessed(exitCode, stdOut, discovery);
        Assert.That(Lines(stdErr), Does.Contain(@"  try: NOT genre == ""metal""").And.Contain(@"  try: ALL(genre) != ""metal"""));
    }

    [TestCase(@"file::size == ""big""", TestName = "A type mismatch is rejected")]
    [TestCase(@"file::name =~ r""(?=a)""", TestName = "A pattern construct the engine does not support is rejected")]
    [TestCase(@"NUMBER(TRUE) > 1", TestName = "A function argument of the wrong type is rejected")]
    [TestCase(@"file::name", TestName = "A predicate that is not a boolean is rejected")]
    public async Task ASemanticError_IsRejectedWith2BeforeAnythingIsProcessed_AndShownAgainstThePredicate(string predicate)
    {
        _collection.Mp3("a.mp3");

        var (exitCode, stdOut, stdErr, discovery) = await RunObservedAsync("list", $"--filter={predicate}", _collection.Root);

        AssertRejectedBeforeAnythingWasProcessed(exitCode, stdOut, discovery);
        var lines = Lines(stdErr);
        Assert.That(lines[0], Does.StartWith("goro: error: "));
        Assert.That(lines[1], Is.EqualTo($"  {predicate}"));
        Assert.That(lines[2], Does.Match(@"^ *\^+$"));
    }

    // D5: past the parser, every independent error is reported at once.
    [Test]
    public async Task TwoIndependentSemanticErrors_AreBothReported()
    {
        var (exitCode, stdOut, stdErr, discovery) = await RunObservedAsync(
            "list", @"--filter=file::nmae == ""a"" AND file::sise > 0", _collection.Root);

        AssertRejectedBeforeAnythingWasProcessed(exitCode, stdOut, discovery);
        Assert.That(Lines(stdErr).Count(l => l.StartsWith("goro: error: ")), Is.EqualTo(2), stdErr);
    }

    // --- which files are kept ---

    [Test]
    public async Task OnlyTheFilesThePredicateHoldsFor_AreListed()
    {
        var a = _collection.Mp3("a.mp3");
        _collection.Mp3("b.mp3");

        var (exitCode, stdOut, stdErr) = await GoroAppFactory.Create().RunCapturedAsync(
            "list", "--strict-exit-code", @"--filter=file::name == ""A.MP3""", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(Lines(stdOut), Is.EqualTo(new[] { a }));
        Assert.That(stdErr, Is.Empty);
    }

    [Test]
    public async Task APredicateHoldingForNoFile_ListsNothing_AndReturns20UnderStrict()
    {
        _collection.Mp3("a.mp3");

        var (exitCode, stdOut, stdErr) = await GoroAppFactory.Create().RunCapturedAsync(
            "list", "--strict-exit-code", "--filter=file::size > 1gb", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(20));
        Assert.That(stdOut, Is.Empty);
        Assert.That(stdErr, Is.Empty);
    }

    [Test]
    public async Task TheAttachedAndSeparateForms_AreTheSame()
    {
        _collection.Mp3("a.mp3");
        _collection.Mp3("b.mp3");
        const string predicate = @"file::name == ""a.mp3""";

        var attached = await GoroAppFactory.Create().RunCapturedAsync("list", $"--filter={predicate}", _collection.Root);
        var separate = await GoroAppFactory.Create().RunCapturedAsync("list", "--filter", predicate, _collection.Root);

        Assert.That(separate, Is.EqualTo(attached));
    }

    [Test]
    public async Task JsonOutput_HoldsOnlyTheFilesThePredicateHoldsFor()
    {
        var a = _collection.Mp3("a.mp3");
        _collection.Mp3("b.mp3");

        var (exitCode, stdOut, _) = await GoroAppFactory.Create().RunCapturedAsync(
            "list", "-o", "json", @"--filter=file::name == ""a.mp3""", _collection.Root);

        Assert.That(exitCode, Is.EqualTo(0));
        using var document = JsonDocument.Parse(stdOut);
        Assert.That(document.RootElement.EnumerateArray().Select(e => e.GetProperty("file").GetString()), Is.EqualTo(new[] { a }));
    }
}
