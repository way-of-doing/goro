using System.Diagnostics;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// Turns a semantic tree into the typed evaluation tree: the last stage of reading a predicate, and
/// the one place where a Goro type known only as data becomes a C# type parameter, through
/// <see cref="TypedNodes"/>.
/// </summary>
/// <remarks>
/// Lowering runs only on a predicate with no errors, and decides nothing: every node becomes the
/// evaluation node it stands for, taking its origin from the sources analysis, its compiled pattern
/// from the patterns analysis and its prepared range from the constants analysis. Identity
/// conversions and the parentheses and modifiers the binder folded away leave nothing behind.
/// </remarks>
public static class Lowering
{
    public static CompiledPredicate Lower(SemanticTree tree, ConstantsAnalysis constants, PatternsAnalysis patterns, SourcesAnalysis sources)
    {
        var lowering = new Lowerer(constants, patterns, sources);
        var root = lowering.Lower(tree.Root) as BoundExpression<bool>
            ?? throw new UnreachableException("A predicate with no errors is a boolean.");
        return new CompiledPredicate(tree.Text, root, sources.Table);
    }

    private sealed class Lowerer(ConstantsAnalysis constants, PatternsAnalysis patterns, SourcesAnalysis sources)
    {
        public BoundExpression Lower(SemanticExpression node) => node switch
        {
            SemanticLiteral literal => TypedNodes.Literal(literal),
            SemanticIdentifier identifier => identifier.Declaration.Bind(sources.OriginOf(identifier)),
            SemanticConversion { IsIdentity: true } conversion => Lower(conversion.Argument),
            SemanticConversion conversion => conversion.Type == GoroType.Number
                ? TypedNodes.Number(Lower(conversion.Argument), sources.OriginOf(conversion))
                : TypedNodes.String(Lower(conversion.Argument), sources.OriginOf(conversion)),
            SemanticCount count => TypedNodes.Count(Lower(count.Argument)),
            SemanticFallback fallback => TypedNodes.Fallback(Lower(fallback.Argument), Lower(fallback.Default)),
            SemanticComparison comparison => TypedNodes.Comparison(
                Lower(comparison.Left.Expression), comparison.Left.Quantifier, comparison.Operator,
                Lower(comparison.Right.Expression), comparison.Right.Quantifier, comparison.Mode),
            SemanticRange range => Analysed(constants.RangeOf(range)).Test(Lower(range.Subject.Expression), range.Subject.Quantifier),
            SemanticMatch match => new RegexMatch(
                new BoundOperand<string>((BoundExpression<string>)Lower(match.Subject.Expression), match.Subject.Quantifier),
                Analysed(patterns.PatternOf(match)), match.Mode),
            SemanticStateTest test => TypedNodes.StateTest(Lower(test.Operand.Expression), test.Operand.Quantifier, test.State),
            SemanticNot not => new Not(Condition(not.Operand)),
            SemanticLogical logical => logical.Operator == Syntax.LogicalOperator.And
                ? new And(Condition(logical.Left), Condition(logical.Right))
                : new Or(Condition(logical.Left), Condition(logical.Right)),
            _ => throw new UnreachableException($"{node.GetType().Name} is never lowered; a tree with errors stops before lowering."),
        };

        private BoundExpression<bool> Condition(SemanticExpression node) => (BoundExpression<bool>)Lower(node);

        private static T Analysed<T>(T? fact) where T : class =>
            fact ?? throw new UnreachableException("An analysis that reported no error has a fact for every node it owns.");
    }
}
