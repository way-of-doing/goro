// Owned by the binder group (G4) of the predicate-runtime-architecture line.
using System.Diagnostics;
using Goro.Predicates.Diagnostics;
using Goro.Predicates.Identifiers;
using Goro.Predicates.Syntax;
using Goro.Predicates.Text;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// One run of static analysis over one syntax tree: a single recursive pass, bottom-up and in text
/// order, that resolves, types, checks and builds as it goes.
/// </summary>
/// <remarks>
/// <para>
/// A sub-expression can stand in one of three kinds of place, and each has a method:
/// <see cref="Condition"/> for the predicate and the operands of <c>NOT</c>, <c>AND</c> and <c>OR</c>;
/// <see cref="Operand"/> for an operand of a comparison, range, regex or state test, the one place
/// a modifier belongs; and <see cref="Value"/> for everything else, function arguments and the
/// inside of an operand among them. Parentheses are transparent to all three.
/// </para>
/// <para>
/// Errors do not stop the pass. A mistake that leaves a sub-expression's type unknown gives it the
/// error type (<see cref="Bound.IsError"/>), and every check that involves it is skipped, so each
/// mistake is reported once, where it was made, and every independent one is reported.
/// </para>
/// </remarks>
internal sealed class Analysis
{
    private readonly string text;
    private readonly IIdentifierCatalog catalog;
    private readonly Sources sources;
    private readonly BinderDiagnostics report;
    private readonly List<Diagnostic> diagnostics = [];

    private Analysis(string text, IIdentifierCatalog catalog)
    {
        this.text = text;
        this.catalog = catalog;
        sources = new Sources(text);
        report = new BinderDiagnostics(text);
    }

    public static StageResult<CompiledPredicate> Run(SyntaxTree tree, IIdentifierCatalog catalog)
    {
        var analysis = new Analysis(tree.Text, catalog);
        var root = analysis.Condition(tree.Root);
        if (analysis.diagnostics.Count > 0)
        {
            // Sorting is stable, so errors that start together stay in the order they were found.
            return StageResult<CompiledPredicate>.Failure(analysis.diagnostics.OrderBy(d => d.Span.Start));
        }

        var node = root.Node as BoundExpression<bool>
            ?? throw new UnreachableException("A predicate that raised no error must have been built.");
        return StageResult<CompiledPredicate>.Success(new CompiledPredicate(tree.Text, node, analysis.sources.Table()));
    }

    // ---------------------------------------------------------------------------------------------
    // The three kinds of place

    /// <summary>Somewhere a definite boolean is required.</summary>
    private Bound Condition(ExpressionSyntax syntax)
    {
        var bound = Unmodified(syntax);
        if (bound.IsError)
        {
            return bound;
        }

        if (bound.Type != GoroType.Boolean)
        {
            Report(report.NotACondition(syntax, bound.Type!.Value));
            return bound with { Node = null };
        }

        if (!bound.IsDefinite)
        {
            Report(report.IndefiniteCondition(syntax));
            return bound with { Node = null };
        }

        return bound;
    }

    /// <summary>An operand of a comparison, range, regex or state test: modifiers are folded here.</summary>
    private OperandOf Operand(ExpressionSyntax syntax)
    {
        ModifierSyntax? quantifier = null;
        ModifierSyntax? literally = null;
        var contradicted = false;
        var core = syntax;
        while (true)
        {
            if (core is ParenthesizedSyntax parentheses)
            {
                core = parentheses.Expression;
            }
            else if (core is ModifierSyntax modifier)
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

                core = modifier.Operand;
            }
            else
            {
                break;
            }
        }

        return new OperandOf(syntax, core, Value(core), quantifier, literally);
    }

    /// <summary>Anywhere a value of any kind may stand, and no modifier may.</summary>
    private Bound Value(ExpressionSyntax syntax) => syntax switch
    {
        ParenthesizedSyntax parentheses => Value(parentheses.Expression),
        LiteralSyntax literal => Literal(literal),
        NullSyntax @null => Null(@null),
        IdentifierSyntax identifier => Identifier(identifier),
        FunctionCallSyntax call => Call(call),
        ComparisonSyntax comparison => Comparison(comparison),
        BetweenSyntax between => Between(between),
        MatchSyntax match => Match(match),
        StateTestSyntax test => StateTest(test),
        NotSyntax not => Not(not),
        LogicalSyntax logical => Logical(logical),
        // Every caller takes modifiers off first; this is only a safety net.
        ModifierSyntax modifier => Unmodified(modifier),
        _ => throw new UnreachableException($"{syntax.GetType().Name} is not an expression the binder knows."),
    };

    /// <summary>
    /// A value where a modifier would be misplaced. One error covers a whole stack of them, and what
    /// they were applied to is still analysed, so that mistakes inside it are found too.
    /// </summary>
    /// <param name="function">The function this is an argument of, if it is one, for the message.</param>
    private Bound Unmodified(ExpressionSyntax syntax, NameToken? function = null)
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
    // Leaves

    private static Bound Literal(LiteralSyntax literal) =>
        new(TypedNodes.TypeOf(literal.Token), true, TypedNodes.Literal(literal.Token), literal, Shape.OfLiteral(literal.Token));

    private Bound Null(NullSyntax @null)
    {
        Report(report.NullValue(@null.Span));
        return Bound.Error;
    }

    private Bound Identifier(IdentifierSyntax identifier)
    {
        // A leading "::" roots the name at the global namespace, which every namespace is inside,
        // so it changes nothing about which identifier is named.
        var name = new IdentifierName(
            identifier.Parts.Take(identifier.Parts.Length - 1).Select(part => part.Text), identifier.Parts[^1].Text);

        switch (catalog.Lookup(name))
        {
            case IdentifierLookup.Found(var declaration):
                var shape = sources.Identifier(declaration.Name);
                var node = declaration.Bind(sources.Origin(shape, identifier.Span));
                return new Bound(declaration.Type, declaration.IsDefinite, node, null, shape);

            case IdentifierLookup.UnknownNamespace(var known):
                Report(report.UnknownNamespace(identifier, known));
                return Bound.Error;

            case IdentifierLookup.UnknownIdentifier(var candidates):
                Report(report.UnknownIdentifier(identifier, candidates));
                return Bound.Error;

            default:
                throw new UnreachableException();
        }
    }

    // ---------------------------------------------------------------------------------------------
    // Functions

    private Bound Call(FunctionCallSyntax call)
    {
        // Every argument is analysed whatever is wrong with the call, for the mistakes inside it.
        var arguments = call.Arguments
            .Select(argument => Unmodified(argument, call.Name))
            .ToList();

        var function = call.Name.Text.ToUpperInvariant();
        int? arity = function switch
        {
            "COUNT" or "NUMBER" or "STRING" => 1,
            "FALLBACK" => 2,
            _ => null,
        };

        if (arity is null)
        {
            Report(report.UnknownFunction(call.Name));
            return Bound.Error;
        }

        if (arguments.Count != arity)
        {
            Report(report.WrongArgumentCount(call, arity.Value));
            return function switch
            {
                "COUNT" or "NUMBER" => new Bound(GoroType.Number, true, null, null, Shape.Unknown),
                "STRING" => new Bound(GoroType.String, true, null, null, Shape.Unknown),
                _ when arguments.Count > 0 => arguments[0] with { Node = null, Literal = null, Shape = Shape.Unknown },
                _ => Bound.Error,
            };
        }

        return function switch
        {
            "COUNT" => Count(arguments[0]),
            "NUMBER" => Number(call, arguments[0]),
            "STRING" => String(call, arguments[0]),
            _ => Fallback(call, arguments[0], arguments[1]),
        };
    }

    private static Bound Count(Bound argument) =>
        new(GoroType.Number, true, argument.Node is { } node ? TypedNodes.Count(node) : null, null, Shape.Call("COUNT", argument.Shape));

    private Bound Number(FunctionCallSyntax call, Bound argument)
    {
        if (argument.IsError)
        {
            return new Bound(GoroType.Number, true, null, null, Shape.Unknown);
        }

        switch (argument.Type)
        {
            case GoroType.Boolean:
                Report(report.BooleanNotConvertible(call));
                return new Bound(GoroType.Number, argument.IsDefinite, null, null, Shape.Unknown);

            case GoroType.Number:
                // Returns its argument, which is therefore no longer a literal as written.
                return argument with { Literal = null };

            case GoroType.String when argument.Literal?.Token is StringToken literal && !NumberText.TryParse(literal.Value, out _):
                Report(report.InvalidNumberLiteral(call, argument.Literal));
                return new Bound(GoroType.Number, true, null, null, Shape.Unknown);

            default:
                return Conversion(call, "NUMBER", GoroType.Number, argument);
        }
    }

    private Bound String(FunctionCallSyntax call, Bound argument)
    {
        if (argument.IsError)
        {
            return new Bound(GoroType.String, true, null, null, Shape.Unknown);
        }

        switch (argument.Type)
        {
            case GoroType.Boolean:
                Report(report.BooleanNotConvertible(call));
                return new Bound(GoroType.String, argument.IsDefinite, null, null, Shape.Unknown);

            case GoroType.String:
                return argument with { Literal = null };

            default:
                return Conversion(call, "STRING", GoroType.String, argument);
        }
    }

    /// <summary>A conversion is a source of its own, interned after its argument's.</summary>
    private Bound Conversion(FunctionCallSyntax call, string function, GoroType result, Bound argument)
    {
        var shape = Shape.Call(function, argument.Shape);
        BoundExpression? node = null;
        if (argument.Node is not null)
        {
            var origin = sources.Origin(shape, call.Span);
            node = result == GoroType.Number ? TypedNodes.Number(argument.Node, origin) : TypedNodes.String(argument.Node, origin);
        }

        return new Bound(result, argument.IsDefinite, node, null, shape);
    }

    private Bound Fallback(FunctionCallSyntax call, Bound argument, Bound @default)
    {
        var buildable = argument.Node is not null;
        if (!@default.IsError && @default.Literal is null)
        {
            Report(report.FallbackDefaultNotLiteral(call));
            buildable = false;
        }
        else if (!argument.IsError && !@default.IsError)
        {
            @default = StandIn(@default, argument.Type!.Value);
            if (@default.Type != argument.Type)
            {
                Report(report.FallbackTypeMismatch(call, argument, @default));
                buildable = false;
            }
        }

        if (argument.IsError)
        {
            return Bound.Error;
        }

        var node = buildable && @default.Node is not null ? TypedNodes.Fallback(argument.Node!, @default.Node) : null;
        return new Bound(argument.Type, argument.IsDefinite, node, null, Shape.Call("FALLBACK", argument.Shape, @default.Shape));
    }

    // ---------------------------------------------------------------------------------------------
    // Operators

    private Bound Comparison(ComparisonSyntax comparison)
    {
        if (NullComparison(comparison) is { } answered)
        {
            return answered;
        }

        var left = Operand(comparison.Left);
        var right = Operand(comparison.Right);
        CheckLiterally(left);
        CheckLiterally(right);

        var (l, r) = (left.Bound, right.Bound);
        if (!l.IsError && !r.IsError && l.Type != r.Type)
        {
            // The numeric literal exception: a number literal takes a bytecount's or a duration's type.
            (l, r) = l.IsNumberLiteral ? (StandIn(l, r.Type!.Value), r) : (l, StandIn(r, l.Type!.Value));
        }

        var buildable = l.Node is not null && r.Node is not null;
        if (!l.IsError && !r.IsError)
        {
            if (l.Type != r.Type)
            {
                Report(report.TypeMismatch(comparison, left.Core, l, right.Core, r));
                buildable = false;
            }
            else if (l.Type == GoroType.Boolean && comparison.Operator is not (ComparisonOperator.Equal or ComparisonOperator.NotEqual))
            {
                Report(report.BooleanCompared(comparison));
                buildable = false;
            }
        }

        if (comparison.Operator == ComparisonOperator.NotEqual)
        {
            var leftNeeds = NeedsQuantifier(left) ? left.Core : null;
            var rightNeeds = NeedsQuantifier(right) ? right.Core : null;
            if (leftNeeds is not null || rightNeeds is not null)
            {
                Report(report.AmbiguousNotEqual(comparison, leftNeeds, rightNeeds));
            }
        }

        var node = buildable
            ? TypedNodes.Comparison(l.Node!, left.Quantifier, comparison.Operator, r.Node!, right.Quantifier, ModeOf(left, right))
            : null;
        return Operator(node, Shape.Operator(Symbol(comparison.Operator), l.Shape, r.Shape));
    }

    /// <summary>
    /// <c>x == NULL</c> or <c>x != NULL</c>, answered with the state test meant; null when the
    /// comparison is not of that kind. Its other operand is still analysed for mistakes of its own.
    /// </summary>
    private Bound? NullComparison(ComparisonSyntax comparison)
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
        return Operator(null, Shape.Unknown);
    }

    private Bound Between(BetweenSyntax between)
    {
        var subject = Operand(between.Subject);
        CheckLiterally(subject);
        var s = subject.Bound;
        var mode = ModeOf(subject);
        var minimum = Literal(between.Minimum);
        var maximum = Literal(between.Maximum);

        var buildable = s.Node is not null;
        if (!s.IsError && s.Type == GoroType.Boolean)
        {
            Report(report.BooleanBetween(subject.Core));
            buildable = false;
        }

        // The ends must agree as written: in 60..120kb it is not clear what 60 was meant to be.
        var rangeIsSound = true;
        if (minimum.Type != maximum.Type)
        {
            Report(IsUnit(minimum.Type) && maximum.IsNumberLiteral || IsUnit(maximum.Type) && minimum.IsNumberLiteral
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
                buildable = false;
            }
        }

        // The ends are checked against each other whatever the subject, compared as the operator will compare them.
        PreparedRange? range = null;
        if (rangeIsSound && minimum.Node is not null && maximum.Node is not null)
        {
            range = TypedNodes.PrepareRange(minimum.Node, maximum.Node, mode);
            if (range.IsReversed)
            {
                Report(report.RangeReversed(between, minimum.Type!.Value, mode));
                range = null;
            }
        }

        var node = buildable && range is not null ? range.Test(s.Node!, subject.Quantifier) : null;
        return Operator(node, Shape.Operator("BETWEEN", s.Shape, $"{minimum.Shape.Key}..{maximum.Shape.Key}"));
    }

    private Bound Match(MatchSyntax match)
    {
        var subject = Operand(match.Subject);
        CheckLiterally(subject);
        var s = subject.Bound;
        var mode = ModeOf(subject);

        var buildable = s.Node is not null;
        if (!s.IsError && s.Type != GoroType.String)
        {
            Report(report.MatchSubjectNotString(subject.Core, s.Type!.Value));
            buildable = false;
        }

        var pattern = Patterns.Compile(match.Pattern.Value, mode, out var failure);
        if (failure is not null)
        {
            Report(report.Pattern(match.Pattern, failure));
        }

        var node = buildable && pattern is not null
            ? new RegexMatch(new BoundOperand<string>((BoundExpression<string>)s.Node!, subject.Quantifier), pattern, mode)
            : null;
        return Operator(node, Shape.Operator("=~", s.Shape, Shape.Quote(match.Pattern.Value)));
    }

    private Bound StateTest(StateTestSyntax test)
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

        var node = operand.Bound.Node is { } expression ? TypedNodes.StateTest(expression, operand.Quantifier, test.State) : null;
        return Operator(node, Shape.Operator("IS", operand.Bound.Shape, test.State.ToString().ToUpperInvariant()));
    }

    private Bound Not(NotSyntax not)
    {
        var operand = Condition(not.Operand);
        var node = operand.Node is BoundExpression<bool> condition ? new Not(condition) : null;
        return Operator(node, Shape.Not(operand.Shape));
    }

    private Bound Logical(LogicalSyntax logical)
    {
        var left = Condition(logical.Left);
        var right = Condition(logical.Right);
        BoundCondition? node = (left.Node, right.Node) switch
        {
            (BoundExpression<bool> l, BoundExpression<bool> r) => logical.Operator == LogicalOperator.And ? new And(l, r) : new Or(l, r),
            _ => null,
        };
        return Operator(node, Shape.Operator(logical.Operator.ToString().ToUpperInvariant(), left.Shape, right.Shape));
    }

    /// <summary>Every operator is a definite boolean, whatever is wrong inside it.</summary>
    private static Bound Operator(BoundExpression? node, Shape shape) => new(GoroType.Boolean, true, node, null, shape);

    // ---------------------------------------------------------------------------------------------
    // Rules shared by the operators

    /// <summary>
    /// The numeric literal exception: a number literal, parenthesized or modified or neither, where
    /// a bytecount or a duration is wanted stands for that many bytes or seconds. It must then be
    /// non-negative, and whole for a duration. Anything else is returned as it is.
    /// </summary>
    private Bound StandIn(Bound bound, GoroType wanted)
    {
        if (!bound.IsNumberLiteral || !IsUnit(wanted))
        {
            return bound;
        }

        var literal = bound.Literal!;
        var value = ((NumberToken)literal.Token).Value;
        BoundExpression? node = null;
        if (value < 0)
        {
            Report(report.NegativeUnitLiteral(literal, wanted));
        }
        else if (wanted == GoroType.Duration && value != decimal.Truncate(value))
        {
            Report(report.FractionalDurationLiteral(literal));
        }
        else
        {
            node = TypedNodes.UnitLiteral(wanted, value);
        }

        return new Bound(wanted, true, node, literal, Shape.OfUnit(wanted, value));
    }

    private void CheckLiterally(OperandOf operand)
    {
        if (operand.Literally is { } literally && !operand.Bound.IsError && operand.Bound.Type != GoroType.String)
        {
            Report(report.LiterallyNotString(literally, operand.Core, operand.Bound.Type!.Value));
        }
    }

    /// <summary>An operand of <c>!=</c> that may be absent or hold several values, with no quantifier written.</summary>
    private static bool NeedsQuantifier(OperandOf operand) =>
        !operand.Bound.IsError && !operand.Bound.IsDefinite && operand.QuantifierModifier is null;

    /// <summary>The mode belongs to the operator: <c>LITERALLY</c> on any operand makes it literal.</summary>
    private static ComparisonMode ModeOf(params OperandOf[] operands) =>
        operands.Any(operand => operand.Literally is not null) ? ComparisonMode.Literal : ComparisonMode.Normalized;

    private static bool IsUnit(GoroType? type) => type is GoroType.ByteCount or GoroType.Duration;

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

    /// <summary>An operand as the operator reads it: its modifiers taken off and remembered.</summary>
    /// <param name="QuantifierModifier">The outermost <c>ALL</c> or <c>ANY</c>, which decides the quantifier.</param>
    /// <param name="Literally">The outermost <c>LITERALLY</c>, which makes the operator literal.</param>
    private sealed record OperandOf(
        ExpressionSyntax Syntax, ExpressionSyntax Core, Bound Bound, ModifierSyntax? QuantifierModifier, ModifierSyntax? Literally)
    {
        public Quantifier Quantifier =>
            QuantifierModifier?.Modifier == ModifierKind.All ? Quantifier.Universal : Quantifier.Existential;
    }
}
