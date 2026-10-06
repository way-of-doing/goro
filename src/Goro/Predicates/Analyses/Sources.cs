using System.Diagnostics;
using Goro.Predicates.Binding;
using Goro.Predicates.Evaluation;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Analyses;

/// <summary>
/// Warning sources: every structurally distinct sub-expression that can give birth to an unusable
/// occurrence -- an identifier reference or a conversion -- gets the next <see cref="SourceId"/>,
/// and every node of the same shape shares it.
/// </summary>
/// <remarks>
/// <para>
/// Children are interned before their parents, which is what lets a parent's shape be its symbol
/// plus its children's shapes. Identifiers are numbered by <see cref="IdentifierName"/> equality, so
/// the spellings that name one identifier -- in any case, quoted or bare, with or without a leading
/// <c>::</c> -- can never make two sources.
/// </para>
/// <para>
/// Nothing here can be wrong, so it reports nothing, and it runs only on a tree that is going to be
/// lowered, which is one without errors.
/// </para>
/// </remarks>
public static class Sources
{
    public static SourcesAnalysis Analyse(SemanticTree tree)
    {
        var interner = new Interner(tree.Text);
        interner.Visit(tree.Root);
        return new SourcesAnalysis(interner.Origins, interner.Table());
    }

    private sealed class Interner(string text)
    {
        private readonly Dictionary<IdentifierName, int> identifiers = [];
        private readonly Dictionary<string, SourceId> ids = new(StringComparer.Ordinal);
        private readonly List<string> forms = [];

        public Dictionary<SemanticExpression, Origin> Origins { get; } = [];

        /// <summary>The shape of a node, its children interned first, in text order.</summary>
        public Shape Visit(SemanticExpression node) => node switch
        {
            SemanticLiteral { StandsIn: true } literal => Shape.OfUnit(literal.Type!.Value, ((NumberToken)literal.Token).Value),
            SemanticLiteral literal => Shape.OfLiteral(literal.Token),
            SemanticIdentifier identifier => Source(identifier, Identifier(identifier.Declaration.Name)),
            SemanticConversion { IsIdentity: true } conversion => Visit(conversion.Argument),
            SemanticConversion conversion => Source(conversion, Shape.Call(conversion.Type == GoroType.Number ? "NUMBER" : "STRING", Visit(conversion.Argument))),
            SemanticCount count => Shape.Call("COUNT", Visit(count.Argument)),
            SemanticFallback fallback => Shape.Call("FALLBACK", Visit(fallback.Argument), Visit(fallback.Default)),
            SemanticPreferred preferred => Shape.Call("PREFERRED", [.. preferred.Arguments.Select(Visit)]),
            SemanticComparison comparison => Shape.Operator(Symbol(comparison.Operator), Visit(comparison.Left.Expression), Visit(comparison.Right.Expression)),
            SemanticRange range => Range(range),
            SemanticMatch match => Shape.Operator("=~", Visit(match.Subject.Expression), Shape.Quote(match.Pattern.Value)),
            SemanticStateTest test => Shape.Operator("IS", Visit(test.Operand.Expression), test.State.ToString().ToUpperInvariant()),
            SemanticNot not => Shape.Not(Visit(not.Operand)),
            SemanticLogical logical => Shape.Operator(logical.Operator.ToString().ToUpperInvariant(), Visit(logical.Left), Visit(logical.Right)),
            _ => throw new UnreachableException($"{node.GetType().Name} has no shape; a tree with errors is never interned."),
        };

        public SourceTable Table() => new([.. forms]);

        private Shape Range(SemanticRange range)
        {
            var subject = Visit(range.Subject.Expression);
            return Shape.Operator("BETWEEN", subject, $"{Visit(range.Minimum).Key}..{Visit(range.Maximum).Key}");
        }

        private Shape Identifier(IdentifierName name)
        {
            if (!identifiers.TryGetValue(name, out var number))
            {
                number = identifiers.Count;
                identifiers.Add(name, number);
            }

            var lowered = new IdentifierName(name.Namespace.Select(Lower), Lower(name.Name));
            return new Shape($"#{number}", lowered.ToString());
        }

        /// <summary>Gives a node of this shape its origin: the source of its shape, and the text it was written as.</summary>
        private Shape Source(SemanticExpression node, Shape shape)
        {
            if (!ids.TryGetValue(shape.Key, out var id))
            {
                id = new SourceId(forms.Count);
                ids.Add(shape.Key, id);
                forms.Add(shape.Form);
            }

            Origins.Add(node, new Origin(id, node.Syntax.Span.Of(text), node.Syntax.Span.Start));
            return shape;
        }

        private static string Lower(string part) => part.ToLowerInvariant();

        private static string Symbol(ComparisonOperator @operator) => @operator switch
        {
            ComparisonOperator.Equal => "==",
            ComparisonOperator.NotEqual => "!=",
            ComparisonOperator.Less => "<",
            ComparisonOperator.LessOrEqual => "<=",
            ComparisonOperator.Greater => ">",
            ComparisonOperator.GreaterOrEqual => ">=",
            _ => throw new UnreachableException(),
        };
    }
}

/// <summary>Where every source of a semantic tree was born, and the table of them all.</summary>
public sealed class SourcesAnalysis
{
    private readonly IReadOnlyDictionary<SemanticExpression, Origin> origins;

    internal SourcesAnalysis(IReadOnlyDictionary<SemanticExpression, Origin> origins, SourceTable table)
    {
        this.origins = origins;
        Table = table;
    }

    public SourceTable Table { get; }

    /// <summary>The origin of an identifier reference or a conversion that is not an identity.</summary>
    public Origin OriginOf(SemanticExpression node) => origins[node];
}
