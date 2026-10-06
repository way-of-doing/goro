using System.Collections.Immutable;
using System.Diagnostics;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;
using Goro.Predicates.Text;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// Establishes what a predicate means: resolves every identifier against the catalog, assigns every
/// sub-expression a type, and folds parentheses and modifiers away, reporting the names that name
/// nothing, the types that do not fit, and the modifiers that are misplaced, contradictory or
/// mistyped. Everything else that is checked statically is checked by an analysis of its own, over
/// the <see cref="SemanticTree"/> this produces.
/// </summary>
/// <remarks>
/// <para>
/// One pass, bottom-up and in text order. A sub-expression can stand in one of three kinds of place,
/// and each has a method: <see cref="Condition"/> for the predicate and the operands of <c>NOT</c>,
/// <c>AND</c> and <c>OR</c>; <see cref="Operand"/> for an operand of a comparison, range, regex or
/// state test, the one place a modifier belongs; and <see cref="Value"/> for everything else,
/// function arguments and the inside of an operand among them. Parentheses are transparent to all three.
/// </para>
/// <para>
/// Errors do not stop the pass. A mistake that leaves a sub-expression's type unknown gives it the
/// error type, and every check that involves it is skipped, so each mistake is reported once, where it
/// was made, and every independent one is reported. The tree is built whole either way.
/// </para>
/// </remarks>
public sealed class Binder
{
    private readonly IIdentifierCatalog catalog;
    private readonly BinderDiagnostics report;
    private readonly List<Diagnostic> diagnostics = [];

    private Binder(string text, IIdentifierCatalog catalog)
    {
        this.catalog = catalog;
        report = new BinderDiagnostics(text);
    }

    public static SemanticTree Bind(SyntaxTree tree, IIdentifierCatalog catalog)
    {
        var binder = new Binder(tree.Text, catalog);
        var root = binder.Condition(tree.Root);
        return new SemanticTree(tree, root, [.. binder.diagnostics]);
    }

    // ---------------------------------------------------------------------------------------------
    // The three kinds of place

    /// <summary>Somewhere a boolean is required; that it must also be exactly one is the cardinality analysis's rule.</summary>
    private SemanticExpression Condition(ExpressionSyntax syntax)
    {
        var condition = Unmodified(syntax);
        if (!condition.IsError && condition.Type != GoroType.Boolean)
        {
            Report(report.NotACondition(syntax, condition.Type!.Value));
        }

        return condition;
    }

    /// <summary>An operand of a comparison, range, regex or state test: modifiers are folded here.</summary>
    private SemanticOperand Operand(ExpressionSyntax syntax)
    {
        ModifierSyntax? quantifier = null;
        ModifierSyntax? literally = null;
        var contradicted = false;

        // Takes the parentheses and modifiers off the outside of an expression, folding each modifier
        // into the operand's, and gives back what is left.
        ExpressionSyntax Fold(ExpressionSyntax expression)
        {
            while (true)
            {
                if (expression is ParenthesizedSyntax parentheses)
                {
                    expression = parentheses.Expression;
                }
                else if (expression is ModifierSyntax modifier)
                {
                    if (modifier.Modifier == ModifierKind.Literally)
                    {
                        literally ??= modifier;
                    }
                    else if (quantifier is null)
                    {
                        quantifier = modifier;
                    }
                    else if (quantifier.Modifier != modifier.Modifier && !contradicted)
                    {
                        Report(report.ContradictoryQuantifiers(modifier, quantifier));
                        contradicted = true;
                    }

                    expression = modifier.Operand;
                }
                else
                {
                    return expression;
                }
            }
        }

        var core = Fold(syntax);

        // Modifiers written on the operand of a conversion are misplaced, and reported where the
        // conversion is bound. They are folded in here as well, as if written outside it, which is
        // where the rewrite offered for them puts them: one misplaced modifier is then one error, and
        // a second is reported only where the rewrite would itself be wrong.
        for (var inner = core; inner is AsSyntax;)
        {
            while (inner is AsSyntax conversion)
            {
                inner = conversion.Operand;
            }

            inner = Fold(inner);
        }

        return new SemanticOperand(syntax, core, Value(core), quantifier, literally);
    }

    /// <summary>Anywhere a value of any kind may stand, and no modifier may.</summary>
    private SemanticExpression Value(ExpressionSyntax syntax) => syntax switch
    {
        ParenthesizedSyntax parentheses => Value(parentheses.Expression),
        LiteralSyntax literal => new SemanticLiteral(literal, SemanticLiteral.TypeOf(literal.Token)),
        NullSyntax @null => Null(@null),
        IdentifierSyntax identifier => Identifier(identifier),
        FunctionCallSyntax call => Call(call),
        AsSyntax conversion => As(conversion),
        ComparisonSyntax comparison => Comparison(comparison),
        BetweenSyntax between => Between(between),
        MatchSyntax match => Match(match),
        StateTestSyntax test => StateTest(test),
        NotSyntax not => new SemanticNot(not, Condition(not.Operand)),
        LogicalSyntax logical => new SemanticLogical(logical, Condition(logical.Left), Condition(logical.Right)),
        // Every caller takes modifiers off first; this is only a safety net.
        ModifierSyntax modifier => Unmodified(modifier),
        _ => throw new UnreachableException($"{syntax.GetType().Name} is not an expression the binder knows."),
    };

    /// <summary>
    /// A value where a modifier would be misplaced. One error covers a whole stack of them, and what
    /// they were applied to is still bound, so that mistakes inside it are found too.
    /// </summary>
    /// <param name="function">The function this is an argument of, if it is one, for the message.</param>
    private SemanticExpression Unmodified(ExpressionSyntax syntax, NameToken? function = null)
    {
        if (ModifierWithin(syntax) is not { } modifier)
        {
            return Value(syntax);
        }

        var unmodified = WithoutModifiers(syntax);
        Report(report.MisplacedModifier(modifier, syntax, unmodified, function));
        return Value(unmodified);
    }

    // ---------------------------------------------------------------------------------------------
    // Names

    private SemanticInvalid Null(NullSyntax @null)
    {
        Report(report.NullValue(@null.Span));
        return new SemanticInvalid(@null, null, []);
    }

    private SemanticExpression Identifier(IdentifierSyntax identifier)
    {
        // A leading "::" roots the name at the global namespace, which every namespace is inside,
        // so it changes nothing about which identifier is named.
        var name = new IdentifierName(
            identifier.Parts.Take(identifier.Parts.Length - 1).Select(part => part.Text), identifier.Parts[^1].Text);

        switch (catalog.Lookup(name))
        {
            case IdentifierLookup.Found(var declaration):
                return new SemanticIdentifier(identifier, declaration);

            case IdentifierLookup.UnknownNamespace(var known):
                Report(report.UnknownNamespace(identifier, known));
                return new SemanticInvalid(identifier, null, []);

            case IdentifierLookup.UnknownIdentifier(var candidates):
                Report(report.UnknownIdentifier(identifier, candidates));
                return new SemanticInvalid(identifier, null, []);

            default:
                throw new UnreachableException();
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Functions

    private SemanticExpression Call(FunctionCallSyntax call)
    {
        // Every argument is bound whatever is wrong with the call, for the mistakes inside it.
        var arguments = call.Arguments
            .Select(argument => Unmodified(argument, call.Name))
            .ToImmutableArray();

        var function = call.Name.Text.ToUpperInvariant();
        if (function == "PREFERRED")
        {
            return Preferred(call, arguments);
        }

        // A conversion spelled as a function, or as SQL spells it: answered with the operator, and
        // bound as the operator, so that the rest of the predicate is checked as if it were written so.
        if (TargetNamed(function) is { } target && arguments.Length == 1)
        {
            Report(report.ConversionCalled(call));
            return Conversion(call, call.Arguments[0], arguments[0], target);
        }

        if (function == "CAST" && call.Arguments is [AsSyntax cast])
        {
            Report(report.CastCalled(call, cast));
            return arguments[0];
        }

        int? arity = function switch
        {
            "COUNT" => 1,
            "FALLBACK" => 2,
            _ => null,
        };

        if (arity is null)
        {
            Report(report.UnknownFunction(call.Name));
            return new SemanticMalformedCall(call, null, null, arguments);
        }

        if (arguments.Length != arity)
        {
            Report(report.WrongArgumentCount(call, arity.Value));
            GoroType? type = function switch
            {
                "COUNT" => GoroType.Number,
                _ => arguments.Length > 0 ? arguments[0].Type : null,
            };
            return new SemanticMalformedCall(call, function, type, arguments);
        }

        return function switch
        {
            "COUNT" => new SemanticCount(call, arguments[0]),
            _ => Fallback(call, arguments[0], arguments[1]),
        };
    }

    // ---------------------------------------------------------------------------------------------
    // Conversions

    /// <summary><c>operand AS target</c>, whose operand takes no modifier, as an argument of a function takes none.</summary>
    private SemanticExpression As(AsSyntax syntax)
    {
        var operand = syntax.Operand;
        if (ModifierWithin(operand) is { } modifier)
        {
            operand = WithoutModifiers(operand);
            Report(report.ModifierOnConversion(modifier, syntax, operand));
        }

        var bound = Value(operand);
        if (TargetNamed(syntax.Target.Text) is not { } target)
        {
            Report(report.UnknownTarget(syntax.Target));
            return new SemanticInvalid(syntax, null, [bound]);
        }

        return Conversion(syntax, operand, bound, target);
    }

    /// <summary>
    /// The rules every conversion keeps, however it was written: a boolean converts to nothing, and a
    /// duration and a bytecount do not convert into each other. A conversion that breaks one is
    /// reported and still has its target's type, so that what is built on it is checked as usual.
    /// </summary>
    private SemanticExpression Conversion(ExpressionSyntax syntax, ExpressionSyntax operandSyntax, SemanticExpression operand, GoroType target)
    {
        if (operand.Type == GoroType.Boolean)
        {
            Report(report.BooleanNotConvertible(operandSyntax));
            return new SemanticInvalid(syntax, target, [operand]);
        }

        if (operand.Type is GoroType.Duration or GoroType.ByteCount && IsUnit(target) && operand.Type != target)
        {
            Report(report.UnitsDoNotConvert(operandSyntax, operand.Type!.Value));
            return new SemanticInvalid(syntax, target, [operand]);
        }

        return new SemanticConversion(syntax, target, operand);
    }

    /// <summary>The type a conversion target names, matched without regard to case; null for anything else.</summary>
    private static GoroType? TargetNamed(string name) => name.ToUpperInvariant() switch
    {
        "NUMBER" => GoroType.Number,
        "STRING" => GoroType.String,
        "DURATION" => GoroType.Duration,
        "BYTECOUNT" => GoroType.ByteCount,
        _ => null,
    };

    private SemanticFallback Fallback(FunctionCallSyntax call, SemanticExpression argument, SemanticExpression @default)
    {
        // A default that is not a constant is the constants analysis's to report, and what type it
        // has is beside the point once it is reported. Only a literal can stand in for a unit.
        if (!argument.IsError && !@default.IsError && @default.IsConstant)
        {
            @default = StandIn(@default, argument.Type!.Value);
            if (@default.Type != argument.Type)
            {
                Report(report.ArgumentTypeMismatch(call, 0, argument, 1, @default));
            }
        }

        return new SemanticFallback(call, argument, @default);
    }

    /// <summary>
    /// <c>PREFERRED()</c>, which takes two or more arguments of one type. The call takes the type of its
    /// first argument that is not a number literal, so that a number literal anywhere among the others
    /// can stand in for a bytecount or a duration, as it does beside an operator; each argument that
    /// still does not agree is reported where it was written.
    /// </summary>
    private SemanticExpression Preferred(FunctionCallSyntax call, ImmutableArray<SemanticExpression> arguments)
    {
        var expected = FirstIndex(arguments, argument => !argument.IsError && argument is not SemanticLiteral { IsNumber: true });
        if (expected < 0)
        {
            expected = FirstIndex(arguments, argument => !argument.IsError);
        }

        GoroType? type = expected < 0 ? null : arguments[expected].Type;
        if (type is { } wanted)
        {
            arguments = [.. arguments.Select(argument => argument.IsError ? argument : StandIn(argument, wanted))];
            for (var i = 0; i < arguments.Length; i++)
            {
                if (!arguments[i].IsError && arguments[i].Type != wanted)
                {
                    Report(report.ArgumentTypeMismatch(call, expected, arguments[expected], i, arguments[i]));
                }
            }
        }

        if (arguments.Length < 2)
        {
            Report(report.TooFewArguments(call, 2));
            return new SemanticMalformedCall(call, "PREFERRED", type, arguments);
        }

        return new SemanticPreferred(call, type, arguments);
    }

    // ---------------------------------------------------------------------------------------------
    // Operators

    private SemanticExpression Comparison(ComparisonSyntax comparison)
    {
        if (NullComparison(comparison) is { } answered)
        {
            return answered;
        }

        var left = Operand(comparison.Left);
        var right = Operand(comparison.Right);
        CheckLiterally(left);
        CheckLiterally(right);

        var (l, r) = (left.Expression, right.Expression);
        if (!l.IsError && !r.IsError && l.Type != r.Type)
        {
            // The numeric literal exception: a number literal takes a bytecount's or a duration's type.
            (l, r) = l is SemanticLiteral { IsNumber: true } ? (StandIn(l, r.Type!.Value), r) : (l, StandIn(r, l.Type!.Value));
        }

        if (!l.IsError && !r.IsError)
        {
            if (l.Type != r.Type)
            {
                Report(report.TypeMismatch(comparison, left.Core, l, right.Core, r));
            }
            else if (l.Type == GoroType.Boolean && comparison.Operator is not (ComparisonOperator.Equal or ComparisonOperator.NotEqual))
            {
                Report(report.BooleanCompared(comparison));
            }
        }

        return new SemanticComparison(comparison, left.With(l), comparison.Operator, right.With(r), ModeOf(left, right));
    }

    /// <summary>
    /// <c>x == NULL</c> or <c>x != NULL</c>, answered with the state test meant; null when the
    /// comparison is not of that kind. Its other operand is still bound, for mistakes of its own.
    /// </summary>
    private SemanticInvalid? NullComparison(ComparisonSyntax comparison)
    {
        if (comparison.Operator is not (ComparisonOperator.Equal or ComparisonOperator.NotEqual)
            || (Core(comparison.Left) is NullSyntax) == (Core(comparison.Right) is NullSyntax))
        {
            return null;
        }

        var (@null, other) = Core(comparison.Left) is NullSyntax @left
            ? (left, Operand(comparison.Right))
            : ((NullSyntax)Core(comparison.Right), Operand(comparison.Left));
        Report(report.NullComparison(comparison, @null, other.Core));
        return new SemanticInvalid(comparison, GoroType.Boolean, [other.Expression]);
    }

    private SemanticRange Between(BetweenSyntax between)
    {
        var subject = Operand(between.Subject);
        CheckLiterally(subject);
        var s = subject.Expression;
        var minimum = new SemanticLiteral(between.Minimum, SemanticLiteral.TypeOf(between.Minimum.Token));
        var maximum = new SemanticLiteral(between.Maximum, SemanticLiteral.TypeOf(between.Maximum.Token));

        if (!s.IsError && s.Type == GoroType.Boolean)
        {
            Report(report.BooleanBetween(subject.Core));
        }

        // The ends must agree as written: in 60..120kb it is not clear what 60 was meant to be.
        var rangeIsSound = true;
        if (minimum.Type != maximum.Type)
        {
            Report(IsUnit(minimum.Type) && maximum.IsNumber || IsUnit(maximum.Type) && minimum.IsNumber
                ? report.RangeUnitMissing(between)
                : report.RangeEndpointTypes(between, minimum.Type!.Value, maximum.Type!.Value));
            rangeIsSound = false;
        }
        else if (minimum.Type == GoroType.Boolean)
        {
            Report(report.BooleanRange(between));
            rangeIsSound = false;
        }

        if (rangeIsSound && !s.IsError && s.Type != GoroType.Boolean)
        {
            // A range of numbers stands for bytecounts or durations just as a number literal does.
            minimum = StandIn(minimum, s.Type!.Value);
            maximum = StandIn(maximum, s.Type!.Value);
            if (minimum.Type != s.Type)
            {
                Report(report.RangeTypeMismatch(between, subject.Core, s.Type!.Value, minimum.Type!.Value));
            }
        }

        return new SemanticRange(between, subject, minimum, maximum, ModeOf(subject));
    }

    private SemanticMatch Match(MatchSyntax match)
    {
        var subject = Operand(match.Subject);
        CheckLiterally(subject);
        var s = subject.Expression;
        if (!s.IsError && s.Type != GoroType.String)
        {
            Report(report.MatchSubjectNotString(subject.Core, s.Type!.Value));
        }

        return new SemanticMatch(match, subject, ModeOf(subject));
    }

    private SemanticStateTest StateTest(StateTestSyntax test)
    {
        var operand = Operand(test.Operand);
        if (operand.Literally is { } literally)
        {
            Report(report.LiterallyOnStateTest(literally));
        }

        if (test.State == TestedState.Absent && operand.QuantifierModifier is { } quantifier)
        {
            Report(report.QuantifierOnAbsentTest(quantifier));
        }

        return new SemanticStateTest(test, operand);
    }

    // ---------------------------------------------------------------------------------------------
    // Rules shared by the operators

    /// <summary>
    /// The numeric literal exception: a number literal, parenthesized or modified or neither, where a
    /// bytecount or a duration is wanted stands for that many bytes or seconds. Anything else is
    /// returned as it is. That the number must then be one a bytecount or a duration can hold is a
    /// rule about its value, and the constants analysis's to check.
    /// </summary>
    private static SemanticExpression StandIn(SemanticExpression expression, GoroType wanted) =>
        expression is SemanticLiteral literal ? StandIn(literal, wanted) : expression;

    private static SemanticLiteral StandIn(SemanticLiteral literal, GoroType wanted) =>
        literal.IsNumber && IsUnit(wanted) ? new SemanticLiteral(literal.Syntax, wanted) : literal;

    private void CheckLiterally(SemanticOperand operand)
    {
        if (operand.Literally is { } literally && !operand.Expression.IsError && operand.Expression.Type != GoroType.String)
        {
            Report(report.LiterallyNotString(literally, operand.Core, operand.Expression.Type!.Value));
        }
    }

    /// <summary>The mode belongs to the operator: <c>LITERALLY</c> on any operand makes it literal.</summary>
    private static ComparisonMode ModeOf(params SemanticOperand[] operands) =>
        operands.Any(operand => operand.Literally is not null) ? ComparisonMode.Literal : ComparisonMode.Normalized;

    private static int FirstIndex(ImmutableArray<SemanticExpression> expressions, Func<SemanticExpression, bool> predicate)
    {
        for (var i = 0; i < expressions.Length; i++)
        {
            if (predicate(expressions[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsUnit(GoroType? type) => type is GoroType.ByteCount or GoroType.Duration;

    // ---------------------------------------------------------------------------------------------
    // Looking through parentheses and modifiers

    /// <summary>What an expression is with its parentheses and modifiers taken off.</summary>
    private static ExpressionSyntax Core(ExpressionSyntax syntax) => syntax switch
    {
        ParenthesizedSyntax parentheses => Core(parentheses.Expression),
        ModifierSyntax modifier => Core(modifier.Operand),
        _ => syntax,
    };

    /// <summary>The outermost modifier of an expression, looking through parentheses.</summary>
    private static ModifierSyntax? ModifierWithin(ExpressionSyntax syntax) => syntax switch
    {
        ParenthesizedSyntax parentheses => ModifierWithin(parentheses.Expression),
        ModifierSyntax modifier => modifier,
        _ => null,
    };

    /// <summary>The expression with its stack of modifiers taken off, and any parentheses inside it kept.</summary>
    private static ExpressionSyntax WithoutModifiers(ExpressionSyntax syntax) => syntax switch
    {
        ParenthesizedSyntax parentheses when ModifierWithin(parentheses) is not null => WithoutModifiers(parentheses.Expression),
        ModifierSyntax modifier => WithoutModifiers(modifier.Operand),
        _ => syntax,
    };

    private void Report(Diagnostic diagnostic) => diagnostics.Add(diagnostic);
}
