using System.Runtime.CompilerServices;
using Goro.Predicates;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Values;

namespace Goro.Tests.Predicates.Evaluation.Support;

/// <summary>
/// Plays the binder's part for one test: declares identifiers holding canned bags, and interns the
/// warning sources of identifiers and conversions by their canonical form, as the binder would.
/// Then evaluates any tree built from them against one file.
/// </summary>
internal sealed class TestPredicate
{
    private readonly Dictionary<string, SourceId> sources = new(StringComparer.Ordinal);
    private readonly ConditionalWeakTable<Expression, string> canonicalForms = [];

    public int SourceCount => sources.Count;

    public FileData File { get; } = new("any");

    /// <summary>An identifier, quoted in warnings as <paramref name="name"/>, holding the given bag.</summary>
    public IdentifierReference<T> Id<T>(string name, params Occurrence<T>[] occurrences) where T : notnull =>
        Id(name, new CannedBinding<T>([.. occurrences]));

    public IdentifierReference<T> Id<T>(string name, IdentifierBinding<T> binding) where T : notnull
    {
        var declaration = new IdentifierDeclaration<T>(IdentifierName.Plain(name), Bounds.Any, binding);
        return Register(new IdentifierReference<T>(declaration, OriginOf(name.ToLowerInvariant(), name)), name.ToLowerInvariant());
    }

    /// <summary>
    /// Another mention of the same identifier, written as <paramref name="text"/> starting at
    /// <paramref name="start"/>: the same source. Every other node is taken to start at 0.
    /// </summary>
    public IdentifierReference<T> Mention<T>(IdentifierReference<T> identifier, string text, int start) where T : notnull =>
        Register(new IdentifierReference<T>(identifier.Declaration, new Origin(identifier.Origin.Source, text, start)), CanonicalForm(identifier));

    public Conversion<string, decimal> Number(Expression<string> argument, string? text = null) =>
        Convert<string, decimal>(argument, Conversions.NumberFromString, "NUMBER", text);

    public Conversion<ByteCount, decimal> Number(Expression<ByteCount> argument) =>
        Convert<ByteCount, decimal>(argument, Conversions.NumberFromByteCount, "NUMBER", null);

    public Conversion<Duration, decimal> Number(Expression<Duration> argument) =>
        Convert<Duration, decimal>(argument, Conversions.NumberFromDuration, "NUMBER", null);

    public Conversion<string, Duration> Duration(Expression<string> argument) =>
        Convert<string, Duration>(argument, Conversions.DurationFromString, "DURATION", null);

    public Conversion<string, ByteCount> ByteCount(Expression<string> argument) =>
        Convert<string, ByteCount>(argument, Conversions.ByteCountFromString, "BYTECOUNT", null);

    public Conversion<decimal, string> String(Expression<decimal> argument) =>
        Convert<decimal, string>(argument, Conversions.StringFromNumber, "STRING", null);

    public Conversion<ByteCount, string> String(Expression<ByteCount> argument) =>
        Convert<ByteCount, string>(argument, Conversions.StringFromByteCount, "STRING", null);

    public Conversion<Duration, string> String(Expression<Duration> argument) =>
        Convert<Duration, string>(argument, Conversions.StringFromDuration, "STRING", null);

    public Outcome Decide(Expression<bool> predicate)
    {
        var context = new EvaluationContext(File, SourceCount);
        var truth = new CompiledPredicate("(test)", predicate, SourceTable.Empty).Evaluate(context);
        return new(truth, context.Reported);
    }

    public (Value<T> Value, IReadOnlyList<Origin> Reported) Evaluate<T>(Expression<T> expression) where T : notnull
    {
        var context = new EvaluationContext(File, SourceCount);
        return (expression.Evaluate(context), context.Reported);
    }

    public SourceId SourceOf(string canonicalForm) => sources[canonicalForm];

    private Conversion<TFrom, TTo> Convert<TFrom, TTo>(
        Expression<TFrom> argument, DatumConversion<TFrom, TTo> convert, string function, string? text)
        where TFrom : notnull where TTo : notnull
    {
        var canonical = $"{CanonicalForm(argument)} AS {function}";
        return Register(new Conversion<TFrom, TTo>(argument, convert, OriginOf(canonical, text ?? canonical)), canonical);
    }

    private Origin OriginOf(string canonicalForm, string text)
    {
        if (!sources.TryGetValue(canonicalForm, out var source))
        {
            source = new SourceId(sources.Count);
            sources.Add(canonicalForm, source);
        }

        return new Origin(source, text, 0);
    }

    private TNode Register<TNode>(TNode node, string canonicalForm) where TNode : Expression
    {
        canonicalForms.Add(node, canonicalForm);
        return node;
    }

    private string CanonicalForm(Expression expression) =>
        canonicalForms.TryGetValue(expression, out var form) ? form
        : expression.Apply(new LiteralText()) ?? throw new ArgumentException($"{expression} has no source here.");

    private sealed class LiteralText : IExpressionFunc<string?>
    {
        public string? Invoke<T>(Expression<T> expression) where T : notnull =>
            expression is Literal<T> literal ? $"{literal.Value}" : null;
    }
}

/// <summary>What a predicate decided for a file, and the origins it reported, in order of source.</summary>
internal sealed record Outcome(Truth Truth, IReadOnlyList<Origin> Reported)
{
    public IEnumerable<string> Quoted => Reported.Select(origin => origin.Text);

    public IEnumerable<SourceId> Sources => Reported.Select(origin => origin.Source);
}
