using Goro.Predicates.Syntax;
using static Goro.Tests.Predicates.Syntax.SyntaxAssert;

namespace Goro.Tests.Predicates.Syntax;

/// <summary>The shape of what the parser builds. Its errors are in <see cref="ParserDiagnosticTests"/>.</summary>
public class ParserTests
{
    // ---------------------------------------------------------------------------------------------
    // Precedence and associativity: docs/concepts/predicates.md, Grouping, precedence, and associativity.

    [TestCase("a == 1 AND b == 2 OR c == 3", "(OR (AND (== a 1) (== b 2)) (== c 3))")]
    [TestCase("a == 1 OR b == 2 AND c == 3", "(OR (== a 1) (AND (== b 2) (== c 3)))")]
    [TestCase("NOT a == 1 AND b == 2", "(AND (NOT (== a 1)) (== b 2))")]
    [TestCase("a == 1 AND NOT b == 2", "(AND (== a 1) (NOT (== b 2)))")]
    [TestCase(
        @"artist == ""metallica"" AND year > 2000 OR NOT genre == ""jazz""",
        @"(OR (AND (== artist ""metallica"") (> year 2000)) (NOT (== genre ""jazz"")))")]
    [TestCase(
        @"(genre == ""jazz"" OR genre == ""blues"") AND year < 2000",
        @"(AND (paren (OR (== genre ""jazz"") (== genre ""blues""))) (< year 2000))")]
    public void Precedence_ComparisonsThenNotThenAndThenOr(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    // AS binds more tightly than every operator and chains to the left; its operand may be modified,
    // which the binder rejects, and a target is any name, which the binder judges.
    [TestCase("x AS NUMBER > 5", "(> (AS x NUMBER) 5)")]
    [TestCase("5 < x AS NUMBER", "(< 5 (AS x NUMBER))")]
    [TestCase("x AS NUMBER AS STRING == \"1\"", "(== (AS (AS x NUMBER) STRING) \"1\")")]
    [TestCase("NOT x AS NUMBER > 5", "(NOT (> (AS x NUMBER) 5))")]
    [TestCase("ALL(x AS NUMBER) > 5", "(> (ALL (AS x NUMBER)) 5)")]
    [TestCase("ALL(x) AS NUMBER > 5", "(> (AS (ALL x) NUMBER) 5)")]
    [TestCase("(x) AS NUMBER > 5", "(> (AS (paren x) NUMBER) 5)")]
    [TestCase("COUNT(x AS number) > 5", "(> (call COUNT (AS x number)) 5)")]
    [TestCase("CAST(x AS NUMBER) > 5", "(> (call CAST (AS x NUMBER)) 5)")]
    [TestCase("x as foo > 5", "(> (AS x foo) 5)")]
    [TestCase("x AS NUMBER IS USABLE", "(IS (AS x NUMBER) USABLE)")]
    public void As_BindsTighterThanEveryOperator_AndChains(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    [TestCase("a AND b AND c", "(AND (AND a b) c)")]
    [TestCase("a OR b OR c", "(OR (OR a b) c)")]
    [TestCase("a and b or c and d or e", "(OR (OR (AND a b) (AND c d)) e)")]
    public void Associativity_AndAndOrGroupToTheLeft(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    [TestCase("NOT a", "(NOT a)")]
    [TestCase("NOT NOT a", "(NOT (NOT a))")]
    [TestCase("not Not NOT a == 1", "(NOT (NOT (NOT (== a 1))))")]
    public void Not_IsRepeatable(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    [TestCase("(a == b) == (c == d)", "(== (paren (== a b)) (paren (== c d)))")]
    [TestCase("((a))", "(paren (paren a))")]
    public void Parentheses_AreKeptAsWritten(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    // ---------------------------------------------------------------------------------------------
    // Each comparison tail

    [TestCase("a == b", "(== a b)")]
    [TestCase("a != b", "(!= a b)")]
    [TestCase("a < b", "(< a b)")]
    [TestCase("a <= b", "(<= a b)")]
    [TestCase("a > b", "(> a b)")]
    [TestCase("a >= b", "(>= a b)")]
    [TestCase("a==b", "(== a b)")]
    public void Comparison_EachOperator(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    [TestCase("year BETWEEN 1990..2000", "(BETWEEN year 1990 2000)")]
    [TestCase("x between -5..5", "(BETWEEN x -5 5)")]
    [TestCase(@"artist BETWEEN ""ma"".."" mi""", @"(BETWEEN artist ""ma"" "" mi"")")]
    [TestCase("file::size BETWEEN 10kb..10mb", "(BETWEEN file::size 10000b 10000000b)")]
    [TestCase("file::duration BETWEEN 1:00 .. 2m", "(BETWEEN file::duration 60s 120s)")]
    [TestCase("x BETWEEN TRUE..FALSE", "(BETWEEN x TRUE FALSE)")]
    [TestCase("x BETWEEN 60..120kb", "(BETWEEN x 60 120000b)")]
    public void Between_TakesTwoLiterals(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    [TestCase(@"artist =~ r""^the """, @"(=~ artist r""^the "")")]
    [TestCase(@"LITERALLY(artist) =~ r""otö""", @"(=~ (LITERALLY artist) r""otö"")")]
    public void Match_TakesARawPattern(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    [Test]
    public void Match_PatternReachesTheTreeUnprocessed()
    {
        var match = (MatchSyntax)Parses(@"year =~ r""\d{4}""").Root;
        Assert.That(match.Pattern.Value, Is.EqualTo(@"\d{4}"));
        Assert.That(match.Pattern.IsRaw, Is.True);
    }

    [TestCase("x IS ABSENT", "(IS x ABSENT)")]
    [TestCase("x is usable", "(IS x USABLE)")]
    [TestCase("ALL(x) IS UNUSABLE", "(IS (ALL x) UNUSABLE)")]
    [TestCase("(x AS NUMBER > 5) IS UNUSABLE", "(IS (paren (> (AS x NUMBER) 5)) UNUSABLE)")]
    [TestCase("NOT (ALL(x) IS USABLE)", "(NOT (paren (IS (ALL x) USABLE)))")]
    public void StateTest_TakesAStateName(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    // ---------------------------------------------------------------------------------------------
    // Operands

    [TestCase("ALL(genre) == \"x\"", "(== (ALL genre) \"x\")")]
    [TestCase("(ALL(genre)) == \"x\"", "(== (paren (ALL genre)) \"x\")")]
    [TestCase("LITERALLY(ALL(genre)) != \"x\"", "(!= (LITERALLY (ALL genre)) \"x\")")]
    [TestCase("ALL(ANY(genre)) == 1", "(== (ALL (ANY genre)) 1)")]
    [TestCase("x == LITERALLY(y)", "(== x (LITERALLY y))")]
    [TestCase("ALL((a == b)) == TRUE", "(== (ALL (paren (== a b))) TRUE)")]
    [TestCase("LITERALLY(FALLBACK(genre, \"pop\")) == \"Pop\"", "(== (LITERALLY (call FALLBACK genre \"pop\")) \"Pop\")")]
    public void Modifier_StacksAndKeepsItsPlace(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    // Modifier placement beyond the grammar is the binder's: these parse, and are rejected there.
    [TestCase("COUNT(ALL(genre))", "(call COUNT (ALL genre))")]
    [TestCase("COUNT((ALL(genre)))", "(call COUNT (paren (ALL genre)))")]
    [TestCase("FALLBACK(LITERALLY(genre), \"pop\")", "(call FALLBACK (LITERALLY genre) \"pop\")")]
    [TestCase("ALL(genre)", "(ALL genre)")]
    public void Modifier_AnywhereAnOperandCanBe_Parses(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    [TestCase("COUNT(genre) > 1", "(> (call COUNT genre) 1)")]
    [TestCase("f()", "(call f)")]
    [TestCase("FALLBACK(year < 2000, TRUE)", "(call FALLBACK (< year 2000) TRUE)")]
    [TestCase("f(a, b OR c, NOT d)", "(call f a (OR b c) (NOT d))")]
    [TestCase("count", "count")]
    [TestCase("vorbis::field(\"MOOD\")", "(call vorbis::field \"MOOD\")")]
    [TestCase("id3v2::field(\"TXXX\", r\"MOOD\")", "(call id3v2::field \"TXXX\" r\"MOOD\")")]
    [TestCase("foo::count(x)", "(call foo::count x)")]
    [TestCase("number(\"1\") == 1", "(== (call number \"1\") 1)")]
    public void FunctionCall_IsANameFollowedByAParenthesis(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    [TestCase("artist", "artist")]
    [TestCase("id3v2::TIT2", "id3v2::TIT2")]
    [TestCase("File::Size", "File::Size")]
    [TestCase("foo::bar", "foo::bar")]
    public void Identifier_AsWritten(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }

    [TestCase("r == 1", "(== r 1)")]
    [TestCase("r==1", "(== r 1)")]
    [TestCase("r::x == 1", "(== r::x 1)")]
    [TestCase("x::r == 1", "(== x::r 1)")]
    public void Identifier_NamedR_IsNotARawStringPrefix(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
    }


    [TestCase("TRUE", "TRUE")]
    [TestCase("true", "TRUE")]
    [TestCase("False", "FALSE")]
    [TestCase("x == TRUE", "(== x TRUE)")]
    public void Boolean_IsALiteralWhateverItsCase(string text, string tree)
    {
        Assert.That(Tree(text), Is.EqualTo(tree));
        Assert.That(Parses(text).Root, Is.InstanceOf<LiteralSyntax>().Or.InstanceOf<ComparisonSyntax>());
    }

    [Test]
    public void Null_ParsesAsItsOwnNode()
    {
        var comparison = (ComparisonSyntax)Parses("year == NULL").Root;
        Assert.That(comparison.Right, Is.TypeOf<NullSyntax>());
        Assert.That(Tree("year == null"), Is.EqualTo("(== year NULL)"));
    }

    // The bounds, typing and literal-ness of these are the binder's; the parser must accept them.
    [TestCase("genre != \"x\"")]
    [TestCase("LITERALLY(genre) != \"x\"")]
    [TestCase("FALLBACK(genre, \"\") != \"x\"")]
    [TestCase("ALL(a) != b")]
    [TestCase("file::extension != \"flac\"")]
    [TestCase("ANY(genre) != \"x\"")]
    [TestCase("COUNT(genre) != 1")]
    [TestCase("file::size != 0")]
    [TestCase("(a == b) != (c == d)")]
    [TestCase("FALLBACK(year, (0))")]
    [TestCase("file::duration > (90)")]
    [TestCase("(\"x\") AS NUMBER")]
    [TestCase("artist")]
    [TestCase("5 AND x")]
    [TestCase("x == 5AND y == 1")]
    [TestCase("-5BETWEEN -10..0")]
    public void Parses_WhatOnlyTheBinderCanJudge(string text)
    {
        Parses(text);
    }

    [Test]
    public void LongestToken_LiteralFollowedByKeyword_IsTwoTokens()
    {
        Assert.That(Tree("x == 5AND y == 1"), Is.EqualTo("(AND (== x 5) (== y 1))"));
        Assert.That(Tree("-5BETWEEN -10..0"), Is.EqualTo("(BETWEEN -5 -10 0)"));
    }

    // ---------------------------------------------------------------------------------------------
    // Whitespace

    [TestCase("year == 2000")]
    [TestCase("year　==　2000")]
    [TestCase("year\t==\n2000")]
    [TestCase("  year  ==  2000  ")]
    [TestCase("year==2000")]
    public void Whitespace_OfAnyKind_SeparatesTokens(string text)
    {
        Assert.That(Tree(text), Is.EqualTo(Tree("year == 2000")));
    }

    // ---------------------------------------------------------------------------------------------
    // Spans

    [Test]
    public void EveryNode_SpansWhatItWasReadFrom()
    {
        const string text = " NOT ( ALL( genre ) == \"x\" ) AND COUNT( a, b ) BETWEEN 1 .. 2 OR t =~ r\"y\" OR s IS ABSENT ";
        var root = Parses(text).Root;

        var spans = new List<string>();
        Collect(root);
        Assert.That(spans, Is.EqualTo(new[]
        {
            "NOT ( ALL( genre ) == \"x\" ) AND COUNT( a, b ) BETWEEN 1 .. 2 OR t =~ r\"y\" OR s IS ABSENT",
            "NOT ( ALL( genre ) == \"x\" ) AND COUNT( a, b ) BETWEEN 1 .. 2 OR t =~ r\"y\"",
            "NOT ( ALL( genre ) == \"x\" ) AND COUNT( a, b ) BETWEEN 1 .. 2",
            "NOT ( ALL( genre ) == \"x\" )",
            "( ALL( genre ) == \"x\" )",
            "ALL( genre ) == \"x\"",
            "ALL( genre )",
            "genre",
            "\"x\"",
            "COUNT( a, b ) BETWEEN 1 .. 2",
            "COUNT( a, b )",
            "a",
            "b",
            "1",
            "2",
            "t =~ r\"y\"",
            "t",
            "s IS ABSENT",
            "s",
        }));

        void Collect(ExpressionSyntax node)
        {
            spans.Add(node.Span.Of(text));
            switch (node)
            {
                case LogicalSyntax logical: Collect(logical.Left); Collect(logical.Right); break;
                case NotSyntax not: Collect(not.Operand); break;
                case ParenthesizedSyntax parentheses: Collect(parentheses.Expression); break;
                case ComparisonSyntax comparison: Collect(comparison.Left); Collect(comparison.Right); break;
                case ModifierSyntax modifier: Collect(modifier.Operand); break;
                case FunctionCallSyntax call: call.Arguments.ToList().ForEach(Collect); break;
                case BetweenSyntax between: Collect(between.Subject); Collect(between.Minimum); Collect(between.Maximum); break;
                case MatchSyntax match: Collect(match.Subject); break;
                case StateTestSyntax test: Collect(test.Operand); break;
            }
        }
    }

    [Test]
    public void Comparison_KeepsTheSpanOfItsOperator()
    {
        const string text = "a  !=  b";
        var comparison = (ComparisonSyntax)Parses(text).Root;
        Assert.That(comparison.OperatorSpan.Of(text), Is.EqualTo("!="));
    }

    [Test]
    public void Identifier_KeepsItsSourceAndName_WithTheirSpans()
    {
        const string text = "Ape::Artist";
        var identifier = (IdentifierSyntax)Parses(text).Root;
        Assert.That(identifier.Span.Of(text), Is.EqualTo(text));
        Assert.That(identifier.Source!.Span.Of(text), Is.EqualTo("Ape"));
        Assert.That(identifier.Name.Span.Of(text), Is.EqualTo("Artist"));
    }

    [Test]
    public void FunctionCall_WithASource_KeepsItAndSpansFromIt()
    {
        const string text = "vorbis::field(\"MOOD\")";
        var call = (FunctionCallSyntax)Parses(text).Root;
        Assert.That(call.Span.Of(text), Is.EqualTo(text));
        Assert.That(call.Source!.Text, Is.EqualTo("vorbis"));
        Assert.That(call.Name.Text, Is.EqualTo("field"));
    }

    [Test]
    public void FunctionCall_KeepsItsNameAsWritten()
    {
        var call = (FunctionCallSyntax)Parses("Count(x)").Root;
        Assert.That(call.Name.Text, Is.EqualTo("Count"));
    }
}
