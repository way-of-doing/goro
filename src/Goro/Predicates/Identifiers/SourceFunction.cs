using System.Collections.Immutable;
using Goro.Predicates.Values;

namespace Goro.Predicates.Identifiers;

/// <summary>
/// A function that reads a source's own data by the name the source gives it, such as
/// <c>vorbis::field()</c>. Its arguments are literals, so a call of it is resolved completely when
/// the predicate is read, into a declaration like an identifier's.
/// </summary>
public abstract class SourceFunction
{
    private protected SourceFunction(string source, string name, int minimumArguments, int maximumArguments)
    {
        Source = source;
        Name = name;
        MinimumArguments = minimumArguments;
        MaximumArguments = maximumArguments;
    }

    public string Source { get; }

    public string Name { get; }

    public abstract GoroType Type { get; }

    public int MinimumArguments { get; }

    public int MaximumArguments { get; }

    /// <summary>
    /// The declaration a call with these arguments reads, or what is wrong with one of them. The
    /// binder has checked their number already.
    /// </summary>
    public abstract SourceFunctionResolution Resolve(ImmutableArray<string> arguments);
}

/// <param name="Check">
/// Accepts the arguments, giving them back in canonical form, or rejects one of them; null accepts
/// any arguments as written.
/// </param>
public sealed class SourceFunction<T>(
    string source,
    string name,
    int minimumArguments,
    int maximumArguments,
    Bounds bounds,
    Func<SourceCallName, IdentifierBinding<T>> binding,
    Func<ImmutableArray<string>, ArgumentCheck>? check = null)
    : SourceFunction(source, name, minimumArguments, maximumArguments) where T : notnull
{
    public Bounds Bounds { get; } = bounds;

    public override GoroType Type => GoroTypes.Of<T>();

    public override SourceFunctionResolution Resolve(ImmutableArray<string> arguments)
    {
        switch (check?.Invoke(arguments) ?? new ArgumentCheck.Accepted(arguments))
        {
            case ArgumentCheck.Accepted(var canonical):
                var called = new SourceCallName(Source, Name, canonical);
                return new SourceFunctionResolution.Found(new IdentifierDeclaration<T>(called, Bounds, binding(called)));
            case ArgumentCheck.Rejected rejected:
                return new SourceFunctionResolution.Rejected(rejected.Argument, rejected.Reason);
            default:
                throw new InvalidOperationException("An argument check either accepts or rejects.");
        }
    }
}

public abstract record SourceFunctionResolution
{
    private SourceFunctionResolution()
    {
    }

    public sealed record Found(IdentifierDeclaration Declaration) : SourceFunctionResolution;

    /// <summary>The argument at <paramref name="Argument"/>, counting from zero, is not one the function takes.</summary>
    public sealed record Rejected(int Argument, ArgumentRejection Reason) : SourceFunctionResolution;
}

public abstract record ArgumentCheck
{
    private ArgumentCheck()
    {
    }

    public sealed record Accepted(ImmutableArray<string> Canonical) : ArgumentCheck;

    public sealed record Rejected(int Argument, ArgumentRejection Reason) : ArgumentCheck;
}

/// <summary>Why an argument of a source function was rejected, as data for the binder to word.</summary>
public abstract record ArgumentRejection
{
    private ArgumentRejection()
    {
    }

    /// <summary>Not the shape of an Id3v2 frame identifier.</summary>
    public sealed record MalformedFrame(string Written) : ArgumentRejection;

    /// <summary>A frame Goro reads under another identifier, <paramref name="Renamed"/>.</summary>
    public sealed record FrameRenamed(string Written, string Renamed) : ArgumentRejection;

    /// <summary>A frame that holds no text, given to <c>field()</c>.</summary>
    public sealed record FrameNotText(string Frame) : ArgumentRejection;

    /// <summary>A description given for a frame that carries none.</summary>
    public sealed record DescriptionNotTaken(string Frame) : ArgumentRejection;
}
