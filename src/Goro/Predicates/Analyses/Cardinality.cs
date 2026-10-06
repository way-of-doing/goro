using System.Collections.Immutable;
using System.Diagnostics;
using Goro.Predicates.Binding;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Analyses;

/// <summary>
/// Cardinality bounds, and the rules that read them: the predicate and the operands of the logical
/// operators must be exactly one boolean, and an operand of <c>!=</c> that is not exactly one must
/// have its quantifier written.
/// </summary>
/// <remarks>
/// A sub-expression's bounds say whether it can be absent and whether it can hold several
/// occurrences, and it is exactly one when it can do neither. The error type counts as exactly one, which
/// silences every rule here that would involve it.
/// </remarks>
public static class Cardinality
{
    public static CardinalityAnalysis Analyse(SemanticTree tree)
    {
        var walk = new Walk(tree.Text);
        walk.Visit(tree.Root);
        walk.Condition(tree.Root, tree.Syntax.Root);
        return new CardinalityAnalysis(walk.Bounds, [.. walk.Diagnostics]);
    }

    private sealed class Walk(string text)
    {
        private readonly CardinalityDiagnostics report = new(text);

        public Dictionary<SemanticExpression, Bounds> Bounds { get; } = [];

        public List<Diagnostic> Diagnostics { get; } = [];

        public void Visit(SemanticExpression node)
        {
            // Children first, so that every rule below finds their bounds decided.
            foreach (var child in node.Children)
            {
                Visit(child);
            }

            Bounds[node] = node.IsError ? Values.Bounds.ExactlyOne : node switch
            {
                SemanticLiteral => Values.Bounds.ExactlyOne,
                SemanticIdentifier identifier => identifier.Declaration.Bounds,
                SemanticConversion conversion => Bounds[conversion.Argument],
                SemanticCount => Values.Bounds.ExactlyOne,
                SemanticFallback fallback => Bounds[fallback.Argument].Fallback(),
                SemanticPreferred preferred => Values.Bounds.Preferred(preferred.Arguments.Select(argument => Bounds[argument])),
                // A FALLBACK with the wrong number of arguments still has its argument's bounds, made present.
                SemanticMalformedCall { Function: "FALLBACK", Arguments: [var argument, ..] } => Bounds[argument].Fallback(),
                // A PREFERRED with too few arguments still has the bounds they give.
                SemanticMalformedCall { Function: "PREFERRED", Arguments: [_, ..] } malformed =>
                    Values.Bounds.Preferred(malformed.Arguments.Select(argument => Bounds[argument])),
                SemanticMalformedCall => Values.Bounds.ExactlyOne,
                SemanticInvalid => Values.Bounds.ExactlyOne,
                SemanticOperator => Values.Bounds.ExactlyOne,
                _ => throw new UnreachableException($"{node.GetType().Name} is not a node cardinality knows."),
            };

            switch (node)
            {
                case SemanticNot not:
                    Condition(not.Operand, not.Syntax.Operand);
                    break;
                case SemanticLogical logical:
                    Condition(logical.Left, logical.Syntax.Left);
                    Condition(logical.Right, logical.Syntax.Right);
                    break;
                case SemanticComparison { Operator: ComparisonOperator.NotEqual } comparison:
                    NotEqual(comparison);
                    break;
            }
        }

        /// <summary>A boolean where a condition is wanted; one that is not a boolean is the binder's to report.</summary>
        /// <param name="written">The condition as written, parentheses and all.</param>
        public void Condition(SemanticExpression condition, ExpressionSyntax written)
        {
            if (condition.Type == GoroType.Boolean && Bounds[condition] != Values.Bounds.ExactlyOne)
            {
                Diagnostics.Add(report.ConditionNotExactlyOne(written));
            }
        }

        /// <summary>
        /// An operand that is not exactly one needs its quantifier written. Where one that needs it can
        /// hold several occurrences, the diagnostic offers the quantified readings; where none can, a
        /// quantifier would have nothing to choose among, and the diagnostic is about absence instead.
        /// </summary>
        private void NotEqual(SemanticComparison comparison)
        {
            var leftNeeds = NeedsQuantifier(comparison.Left);
            var rightNeeds = NeedsQuantifier(comparison.Right);
            if (!leftNeeds && !rightNeeds)
            {
                return;
            }

            var (left, right) = (leftNeeds ? comparison.Left.Core : null, rightNeeds ? comparison.Right.Core : null);
            var several = (leftNeeds && Bounds[comparison.Left.Expression].CanBeSeveral)
                || (rightNeeds && Bounds[comparison.Right.Expression].CanBeSeveral);
            Diagnostics.Add(several
                ? report.AmbiguousNotEqual(comparison.Syntax, left, right)
                : report.MayBeAbsentNotEqual(comparison.Syntax, left, right, (leftNeeds ? comparison.Left : comparison.Right).Expression.Type!.Value));
        }

        /// <summary>An operand that may be absent or hold several values, with no quantifier written.</summary>
        private bool NeedsQuantifier(SemanticOperand operand) =>
            Bounds[operand.Expression] != Values.Bounds.ExactlyOne && operand.QuantifierModifier is null;
    }
}

/// <summary>The bounds of each node of a semantic tree, and the errors that follow from them.</summary>
public sealed class CardinalityAnalysis
{
    private readonly IReadOnlyDictionary<SemanticExpression, Bounds> bounds;

    internal CardinalityAnalysis(IReadOnlyDictionary<SemanticExpression, Bounds> bounds, ImmutableArray<Diagnostic> diagnostics)
    {
        this.bounds = bounds;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>Whether <paramref name="node"/> can be absent, and whether it can hold several occurrences.</summary>
    public Bounds BoundsOf(SemanticExpression node) => bounds[node];
}
