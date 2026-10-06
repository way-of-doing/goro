using System.Collections.Immutable;
using System.Diagnostics;
using Goro.Predicates.Binding;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;

namespace Goro.Predicates.Analyses;

/// <summary>
/// Definiteness, and the rules that read it: the predicate and the operands of the logical operators
/// must be definite booleans, and an operand of <c>!=</c> that is not definite must have its
/// quantifier written.
/// </summary>
/// <remarks>
/// A sub-expression is definite when it has exactly one occurrence for every file. The error type
/// counts as definite, which silences every rule here that would involve it.
/// </remarks>
public static class Cardinality
{
    public static CardinalityAnalysis Analyse(SemanticTree tree)
    {
        var walk = new Walk(tree.Text);
        walk.Visit(tree.Root);
        walk.Condition(tree.Root, tree.Syntax.Root);
        return new CardinalityAnalysis(walk.Definite, [.. walk.Diagnostics]);
    }

    private sealed class Walk(string text)
    {
        private readonly CardinalityDiagnostics report = new(text);

        public Dictionary<SemanticExpression, bool> Definite { get; } = [];

        public List<Diagnostic> Diagnostics { get; } = [];

        public void Visit(SemanticExpression node)
        {
            // Children first, so that every rule below finds their definiteness decided.
            foreach (var child in node.Children)
            {
                Visit(child);
            }

            var definite = node.IsError || node switch
            {
                SemanticLiteral => true,
                SemanticIdentifier identifier => identifier.Declaration.IsDefinite,
                SemanticConversion conversion => Definite[conversion.Argument],
                SemanticCount => true,
                SemanticFallback fallback => Definite[fallback.Argument],
                SemanticPreferred preferred => preferred.Arguments.All(argument => Definite[argument]),
                // A FALLBACK with the wrong number of arguments is still as definite as its argument.
                SemanticMalformedCall { Function: "FALLBACK", Arguments: [var argument, ..] } => Definite[argument],
                // A PREFERRED with too few arguments is still definite when they all are.
                SemanticMalformedCall { Function: "PREFERRED" } malformed => malformed.Arguments.All(argument => Definite[argument]),
                SemanticMalformedCall => true,
                SemanticInvalid => true,
                SemanticOperator => true,
                _ => throw new UnreachableException($"{node.GetType().Name} is not a node cardinality knows."),
            };
            Definite[node] = definite;

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
            if (condition.Type == GoroType.Boolean && !Definite[condition])
            {
                Diagnostics.Add(report.IndefiniteCondition(written));
            }
        }

        private void NotEqual(SemanticComparison comparison)
        {
            var leftNeeds = NeedsQuantifier(comparison.Left) ? comparison.Left.Core : null;
            var rightNeeds = NeedsQuantifier(comparison.Right) ? comparison.Right.Core : null;
            if (leftNeeds is not null || rightNeeds is not null)
            {
                Diagnostics.Add(report.AmbiguousNotEqual(comparison.Syntax, leftNeeds, rightNeeds));
            }
        }

        /// <summary>An operand that may be absent or hold several values, with no quantifier written.</summary>
        private bool NeedsQuantifier(SemanticOperand operand) =>
            !Definite[operand.Expression] && operand.QuantifierModifier is null;
    }
}

/// <summary>Whether each node of a semantic tree is definite, and the errors that follow from it.</summary>
public sealed class CardinalityAnalysis
{
    private readonly IReadOnlyDictionary<SemanticExpression, bool> definite;

    internal CardinalityAnalysis(IReadOnlyDictionary<SemanticExpression, bool> definite, ImmutableArray<Diagnostic> diagnostics)
    {
        this.definite = definite;
        Diagnostics = diagnostics;
    }

    public ImmutableArray<Diagnostic> Diagnostics { get; }

    /// <summary>Whether <paramref name="node"/> has exactly one occurrence for every file.</summary>
    public bool IsDefinite(SemanticExpression node) => definite[node];
}
