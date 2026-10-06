using System.Collections.Immutable;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;
using Goro.Predicates.Text;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// A sub-expression with its names resolved and its type assigned: what the predicate means, as the
/// binder established it, before anything is computed about it.
/// </summary>
/// <remarks>
/// <para>
/// Parentheses and modifiers are gone: parentheses because they only group, modifiers because each
/// has been folded into what reads it, a quantifier into its <see cref="SemanticOperand"/> and
/// <c>LITERALLY</c> into its operator's <see cref="ComparisonMode"/>. Every node keeps the syntax it
/// was bound from, so that an analysis can say where a mistake was made.
/// </para>
/// <para>
/// Nodes are compared by identity, which is what lets an analysis keep a property of each in a
/// table of its own rather than on the node.
/// </para>
/// </remarks>
public abstract class SemanticExpression
{
    private protected SemanticExpression(ExpressionSyntax syntax, GoroType? type)
    {
        Syntax = syntax;
        Type = type;
    }

    /// <summary>What this was bound from, with any parentheses around it taken off.</summary>
    public ExpressionSyntax Syntax { get; }

    /// <summary>
    /// Its Goro type, or null for the error type: something in it was wrong in a way that leaves its
    /// type unknown, and every check that would involve it is skipped, so that one mistake is
    /// reported once rather than at every operator it reaches.
    /// </summary>
    public GoroType? Type { get; }

    public bool IsError => Type is null;

    /// <summary>
    /// Whether its value is known when the predicate is read: a literal, or a conversion of a constant.
    /// Parentheses are gone already, so a parenthesized constant is one too.
    /// </summary>
    public virtual bool IsConstant => false;

    /// <summary>The sub-expressions it is made of, in the order they were written.</summary>
    public abstract IEnumerable<SemanticExpression> Children { get; }

    /// <summary>This node and every node below it, children before their parents, in text order.</summary>
    public IEnumerable<SemanticExpression> DescendantsAndSelf()
    {
        foreach (var child in Children)
        {
            foreach (var node in child.DescendantsAndSelf())
            {
                yield return node;
            }
        }

        yield return this;
    }
}

/// <summary>
/// A literal. Its type is usually its token's, except that a number literal standing for a bytecount
/// or a duration, by the numeric literal exception, has that type instead.
/// </summary>
public sealed class SemanticLiteral(LiteralSyntax syntax, GoroType type) : SemanticExpression(syntax, type)
{
    public new LiteralSyntax Syntax => (LiteralSyntax)base.Syntax;

    public Token Token => Syntax.Token;

    /// <summary>A number literal standing for that many bytes or seconds.</summary>
    public bool StandsIn => Token is NumberToken && Type is GoroType.ByteCount or GoroType.Duration;

    /// <summary>A number literal taken as a number, which is what may stand for a bytecount or a duration.</summary>
    public bool IsNumber => Token is NumberToken && Type == GoroType.Number;

    public override bool IsConstant => true;

    public override IEnumerable<SemanticExpression> Children => [];

    /// <summary>The type of a literal as written.</summary>
    public static GoroType TypeOf(Token literal) => literal switch
    {
        StringToken => GoroType.String,
        NumberToken => GoroType.Number,
        ByteCountToken => GoroType.ByteCount,
        DurationToken => GoroType.Duration,
        { Kind: TokenKind.True or TokenKind.False } => GoroType.Boolean,
        _ => throw new ArgumentOutOfRangeException(nameof(literal), literal.Kind, "Not a literal."),
    };
}

/// <summary>An identifier, resolved to its declaration.</summary>
public sealed class SemanticIdentifier(IdentifierSyntax syntax, IdentifierDeclaration declaration)
    : SemanticExpression(syntax, declaration.Type)
{
    public new IdentifierSyntax Syntax => (IdentifierSyntax)base.Syntax;

    public IdentifierDeclaration Declaration { get; } = declaration;

    public override IEnumerable<SemanticExpression> Children => [];
}

/// <summary>
/// <c>argument AS target</c>, whose type is its target. One whose argument is already of its type is
/// an identity, kept so that <c>5 AS NUMBER</c> is still known not to be a literal. Its syntax is
/// what the user wrote, which for the old spelling <c>NUMBER(x)</c>, reported but bound all the
/// same, is a function call.
/// </summary>
public sealed class SemanticConversion(ExpressionSyntax syntax, GoroType target, SemanticExpression argument)
    : SemanticExpression(syntax, target)
{
    public SemanticExpression Argument { get; } = argument;

    public bool IsIdentity => Argument.Type == Type;

    public override bool IsConstant => Argument.IsConstant;

    public override IEnumerable<SemanticExpression> Children => [Argument];
}

/// <summary><c>COUNT(argument)</c>.</summary>
public sealed class SemanticCount(FunctionCallSyntax syntax, SemanticExpression argument)
    : SemanticExpression(syntax, GoroType.Number)
{
    public new FunctionCallSyntax Syntax => (FunctionCallSyntax)base.Syntax;

    public SemanticExpression Argument { get; } = argument;

    public override IEnumerable<SemanticExpression> Children => [Argument];
}

/// <summary><c>FALLBACK(argument, default)</c>, which has its argument's type.</summary>
public sealed class SemanticFallback(FunctionCallSyntax syntax, SemanticExpression argument, SemanticExpression @default)
    : SemanticExpression(syntax, argument.Type)
{
    public new FunctionCallSyntax Syntax => (FunctionCallSyntax)base.Syntax;

    public SemanticExpression Argument { get; } = argument;

    public SemanticExpression Default { get; } = @default;

    public override IEnumerable<SemanticExpression> Children => [Argument, Default];
}

/// <summary>
/// <c>PREFERRED(arguments)</c>, with two or more arguments, which has the type they agree on: that of
/// its first argument that is not a number literal, a number literal being able to stand in for it.
/// </summary>
public sealed class SemanticPreferred(FunctionCallSyntax syntax, GoroType? type, ImmutableArray<SemanticExpression> arguments)
    : SemanticExpression(syntax, type)
{
    public new FunctionCallSyntax Syntax => (FunctionCallSyntax)base.Syntax;

    public ImmutableArray<SemanticExpression> Arguments { get; } = arguments;

    public override IEnumerable<SemanticExpression> Children => Arguments;
}

/// <summary>
/// A call of a function that does not exist, or with the wrong number of arguments, which the
/// binder has reported. Its arguments are kept, so that the mistakes inside them are found too.
/// </summary>
/// <param name="function">The function called, in capitals, or null if there is no such function.</param>
/// <param name="type">What the call would have produced, where that does not depend on what is missing.</param>
public sealed class SemanticMalformedCall(
    FunctionCallSyntax syntax, string? function, GoroType? type, ImmutableArray<SemanticExpression> arguments)
    : SemanticExpression(syntax, type)
{
    public new FunctionCallSyntax Syntax => (FunctionCallSyntax)base.Syntax;

    public string? Function { get; } = function;

    public ImmutableArray<SemanticExpression> Arguments { get; } = arguments;

    public override IEnumerable<SemanticExpression> Children => Arguments;
}

/// <summary>
/// Something the binder rejected outright, such as a name that names nothing, <c>NULL</c>, or a
/// comparison with <c>NULL</c>, which is a boolean all the same. What it contains is kept, so that the
/// mistakes inside it are found too.
/// </summary>
public sealed class SemanticInvalid(ExpressionSyntax syntax, GoroType? type, ImmutableArray<SemanticExpression> parts)
    : SemanticExpression(syntax, type)
{
    public ImmutableArray<SemanticExpression> Parts { get; } = parts;

    public override IEnumerable<SemanticExpression> Children => Parts;
}

/// <summary>An operand of a comparison, range, regex or state test, with its modifiers folded away.</summary>
/// <param name="syntax">The operand as written, parentheses and modifiers and all.</param>
/// <param name="core">What is left of it with its parentheses and modifiers taken off.</param>
/// <param name="quantifierModifier">The outermost <c>ALL</c> or <c>ANY</c>, which decides the quantifier.</param>
/// <param name="literally">The outermost <c>LITERALLY</c>, which makes the operator literal.</param>
public sealed class SemanticOperand(
    ExpressionSyntax syntax, ExpressionSyntax core, SemanticExpression expression, ModifierSyntax? quantifierModifier, ModifierSyntax? literally)
{
    public ExpressionSyntax Syntax { get; } = syntax;

    public ExpressionSyntax Core { get; } = core;

    public SemanticExpression Expression { get; } = expression;

    public ModifierSyntax? QuantifierModifier { get; } = quantifierModifier;

    public ModifierSyntax? Literally { get; } = literally;

    public Quantifier Quantifier =>
        QuantifierModifier?.Modifier == ModifierKind.All ? Quantifier.Universal : Quantifier.Existential;

    /// <summary>The same operand standing for something else, as a number literal does for a bytecount.</summary>
    public SemanticOperand With(SemanticExpression expression) => new(Syntax, Core, expression, QuantifierModifier, Literally);
}

/// <summary>Every operator is a boolean, whatever is wrong inside it.</summary>
public abstract class SemanticOperator(ExpressionSyntax syntax) : SemanticExpression(syntax, GoroType.Boolean);

public sealed class SemanticComparison(
    ComparisonSyntax syntax, SemanticOperand left, ComparisonOperator @operator, SemanticOperand right, ComparisonMode mode)
    : SemanticOperator(syntax)
{
    public new ComparisonSyntax Syntax => (ComparisonSyntax)base.Syntax;

    public SemanticOperand Left { get; } = left;

    public ComparisonOperator Operator { get; } = @operator;

    public SemanticOperand Right { get; } = right;

    public ComparisonMode Mode { get; } = mode;

    public override IEnumerable<SemanticExpression> Children => [Left.Expression, Right.Expression];
}

/// <summary><c>subject BETWEEN minimum..maximum</c>.</summary>
public sealed class SemanticRange(
    BetweenSyntax syntax, SemanticOperand subject, SemanticLiteral minimum, SemanticLiteral maximum, ComparisonMode mode)
    : SemanticOperator(syntax)
{
    public new BetweenSyntax Syntax => (BetweenSyntax)base.Syntax;

    public SemanticOperand Subject { get; } = subject;

    public SemanticLiteral Minimum { get; } = minimum;

    public SemanticLiteral Maximum { get; } = maximum;

    public ComparisonMode Mode { get; } = mode;

    public override IEnumerable<SemanticExpression> Children => [Subject.Expression, Minimum, Maximum];
}

/// <summary><c>subject =~ pattern</c>.</summary>
public sealed class SemanticMatch(MatchSyntax syntax, SemanticOperand subject, ComparisonMode mode) : SemanticOperator(syntax)
{
    public new MatchSyntax Syntax => (MatchSyntax)base.Syntax;

    public SemanticOperand Subject { get; } = subject;

    public StringToken Pattern => Syntax.Pattern;

    public ComparisonMode Mode { get; } = mode;

    public override IEnumerable<SemanticExpression> Children => [Subject.Expression];
}

/// <summary><c>operand IS PRESENT</c>, <c>IS ABSENT</c> or <c>IS USABLE</c>.</summary>
public sealed class SemanticStateTest(StateTestSyntax syntax, SemanticOperand operand) : SemanticOperator(syntax)
{
    public new StateTestSyntax Syntax => (StateTestSyntax)base.Syntax;

    public SemanticOperand Operand { get; } = operand;

    public TestedState State => Syntax.State;

    public override IEnumerable<SemanticExpression> Children => [Operand.Expression];
}

/// <summary><c>NOT operand</c>. The operand as written is <c>Syntax.Operand</c>.</summary>
public sealed class SemanticNot(NotSyntax syntax, SemanticExpression operand) : SemanticOperator(syntax)
{
    public new NotSyntax Syntax => (NotSyntax)base.Syntax;

    public SemanticExpression Operand { get; } = operand;

    public override IEnumerable<SemanticExpression> Children => [Operand];
}

/// <summary><c>AND</c> or <c>OR</c>. The operands as written are <c>Syntax.Left</c> and <c>Syntax.Right</c>.</summary>
public sealed class SemanticLogical(LogicalSyntax syntax, SemanticExpression left, SemanticExpression right) : SemanticOperator(syntax)
{
    public new LogicalSyntax Syntax => (LogicalSyntax)base.Syntax;

    public SemanticExpression Left { get; } = left;

    public LogicalOperator Operator => Syntax.Operator;

    public SemanticExpression Right { get; } = right;

    public override IEnumerable<SemanticExpression> Children => [Left, Right];
}
