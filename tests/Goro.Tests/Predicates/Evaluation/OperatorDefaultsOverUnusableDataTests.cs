using Goro.Predicates.Evaluation;
using Goro.Predicates.Syntax;
using Goro.Predicates.Values;
using Goro.Tests.Predicates.Evaluation.Support;
using static Goro.Tests.Predicates.Evaluation.Support.Nodes;

namespace Goro.Tests.Predicates.Evaluation;

/// <summary>
/// The operators never answer from unusable data: where a comparison, range or logical operator
/// gives true or false, it gives the same answer whatever the unreadable data had held. That is a
/// property of the operators' three-valued logic, not a guarantee about predicates, so it is
/// asserted over generated predicates built from the operators alone -- comparisons and ranges of
/// numbers, <c>COUNT()</c>, and the logical operators. Left out are the constructs that make a
/// result depend on unusable data by design: state tests, <c>FALLBACK()</c> and <c>PREFERRED()</c>,
/// which observe, replace or route around it, and comparing a condition with a boolean, which turns
/// a false into a true as <c>NOT</c> does.
/// </summary>
public class OperatorDefaultsOverUnusableDataTests
{
    private const int Seed = 20261005;
    private const int Predicates = 400;
    private const int FilesPerPredicate = 12;
    private static readonly decimal[] Domain = [0m, 1m, 2m, 3m, 4m];

    // Within that scope and without NOT, nothing can turn a false into a true, so keeping unusable
    // results or reading them as false selects exactly the same files. Wrapping every operator in FALLBACK(..., FALSE) is
    // what reads them as false.
    [Test]
    public void WithoutNot_ReadingUnusableResultsAsFalse_SelectsTheSameFiles()
    {
        var random = new Random(Seed);
        for (var i = 0; i < Predicates; i++)
            {
                var predicate = Generate(random, depth: 3, allowNot: false);
                for (var j = 0; j < FilesPerPredicate; j++)
                {
                    var file = GenerateFile(random);
                    var kept = Decide(predicate, file, unusableAsFalse: false);
                    var readAsFalse = Decide(predicate, file, unusableAsFalse: true);

                    Assert.That(kept == Truth.True, Is.EqualTo(readAsFalse == Truth.True), $"{predicate} over {Show(file)}");
                }
            }
    }

    // A predicate that answers at all gives the same answer whatever the unreadable data had held:
    // so with NOT too, an answer survives every usable replacement of the unusable occurrences, and
    // the operators never make a predicate true on the strength of data that could not be read.
    [Test]
    public void WithNot_AnAnswer_SurvivesEveryUsableReplacementOfTheUnusableOccurrences()
    {
        var random = new Random(Seed);
        var replacementsChecked = 0;
        for (var i = 0; i < Predicates; i++)
            {
                var predicate = Generate(random, depth: 3, allowNot: true);
                for (var j = 0; j < FilesPerPredicate; j++)
                {
                    var file = GenerateFile(random);
                    var answer = Decide(predicate, file, unusableAsFalse: false);
                    if (answer == Truth.Unusable)
                    {
                        continue;
                    }

                    foreach (var replaced in Replacements(file))
                    {
                        Assert.That(Decide(predicate, replaced, unusableAsFalse: false), Is.EqualTo(answer),
                            $"{predicate} over {Show(file)}, replaced by {Show(replaced)}");
                        replacementsChecked++;
                    }
                }
            }
        Assert.That(replacementsChecked, Is.GreaterThan(1000), "The generator should produce answered predicates over unusable data.");
    }

    private abstract record Node;

    private sealed record ComparisonNode(int Left, bool LeftAll, ComparisonOperator Operator, int? Right, bool RightAll, decimal Literal) : Node
    {
        public override string ToString() =>
            $"{Operand(Left, LeftAll)} {Operator} {(Right is { } right ? Operand(right, RightAll) : Literal)}";
    }

    private sealed record RangeNode(int Subject, bool All, decimal Minimum, decimal Maximum) : Node
    {
        public override string ToString() => $"{Operand(Subject, All)} BETWEEN {Minimum}..{Maximum}";
    }

    private sealed record CountNode(int Argument, ComparisonOperator Operator, decimal Literal) : Node
    {
        public override string ToString() => $"COUNT({Name(Argument)}) {Operator} {Literal}";
    }

    private sealed record NotNode(Node Operand) : Node
    {
        public override string ToString() => $"NOT ({Operand})";
    }

    private sealed record AndNode(Node Left, Node Right) : Node
    {
        public override string ToString() => $"({Left}) AND ({Right})";
    }

    private sealed record OrNode(Node Left, Node Right) : Node
    {
        public override string ToString() => $"({Left}) OR ({Right})";
    }

    private static string Name(int identifier) => $"x{identifier}";

    private static string Operand(int identifier, bool all) => all ? $"ALL({Name(identifier)})" : Name(identifier);

    private static Node Generate(Random random, int depth, bool allowNot)
    {
        var choice = random.Next(depth == 0 ? 3 : allowNot ? 6 : 5);
        return choice switch
        {
            0 => new ComparisonNode(
                random.Next(3), random.Next(3) == 0, RandomOperator(random),
                random.Next(2) == 0 ? random.Next(3) : null, random.Next(3) == 0, RandomDatum(random)),
            1 => RandomRange(random),
            2 => new CountNode(random.Next(3), RandomOperator(random), random.Next(4)),
            3 => new AndNode(Generate(random, depth - 1, allowNot), Generate(random, depth - 1, allowNot)),
            4 => new OrNode(Generate(random, depth - 1, allowNot), Generate(random, depth - 1, allowNot)),
            _ => new NotNode(Generate(random, depth - 1, allowNot)),
        };
    }

    private static RangeNode RandomRange(Random random)
    {
        var a = RandomDatum(random);
        var b = RandomDatum(random);
        return new RangeNode(random.Next(3), random.Next(3) == 0, Math.Min(a, b), Math.Max(a, b));
    }

    private static ComparisonOperator RandomOperator(Random random) =>
        Enum.GetValues<ComparisonOperator>()[random.Next(6)];

    private static decimal RandomDatum(Random random) => Domain[random.Next(Domain.Length)];

    /// <summary>A file: a bag for each of the three identifiers, each absent or of up to three occurrences.</summary>
    private static Occurrence<decimal>[][] GenerateFile(Random random) =>
    [
        .. Enumerable.Range(0, 3).Select(_ => Enumerable.Range(0, random.Next(4))
            .Select(_ => random.Next(3) == 0 ? (Occurrence<decimal>)BadNumber : Ok(RandomDatum(random)))
            .ToArray()),
    ];

    /// <summary>Every way of replacing the file's unusable occurrences with usable ones from the domain.</summary>
    private static IEnumerable<Occurrence<decimal>[][]> Replacements(Occurrence<decimal>[][] file)
    {
        var slots = file.SelectMany((bag, i) => bag.Select((o, k) => (i, k, o))).Where(s => s.o is Unusable<decimal>).ToArray();
        if (slots.Length > 4)
        {
            yield break;
        }

        var choices = new int[slots.Length];
        while (true)
        {
            var replaced = file.Select(bag => bag.ToArray()).ToArray();
            for (var s = 0; s < slots.Length; s++)
            {
                replaced[slots[s].i][slots[s].k] = Ok(Domain[choices[s]]);
            }

            yield return replaced;

            var position = 0;
            while (position < slots.Length && ++choices[position] == Domain.Length)
            {
                choices[position++] = 0;
            }

            if (position == slots.Length)
            {
                yield break;
            }
        }
    }

    private static Truth Decide(Node predicate, Occurrence<decimal>[][] file, bool unusableAsFalse)
    {
        var p = new TestPredicate();
        var identifiers = file.Select((bag, i) => p.Id(Name(i), bag)).ToArray();
        return p.Decide(Build(predicate, identifiers, unusableAsFalse)).Truth;
    }

    private static Expression<bool> Build(Node node, IdentifierReference<decimal>[] identifiers, bool unusableAsFalse)
    {
        Expression<decimal> Quantified(int identifier, bool all) =>
            all ? All(identifiers[identifier]) : identifiers[identifier];

        Expression<bool> Operator(Expression<bool> condition) =>
            unusableAsFalse ? Fallback(condition, false) : condition;

        return node switch
        {
            ComparisonNode c => Operator(Compare(
                Quantified(c.Left, c.LeftAll),
                c.Operator,
                c.Right is { } right ? Quantified(right, c.RightAll) : Lit(c.Literal))),
            RangeNode r => Operator(Between(Quantified(r.Subject, r.All), r.Minimum, r.Maximum)),
            CountNode c => Operator(Compare(Count(identifiers[c.Argument]), c.Operator, Lit(c.Literal))),
            NotNode n => Not(Build(n.Operand, identifiers, unusableAsFalse)),
            AndNode a => And(Build(a.Left, identifiers, unusableAsFalse), Build(a.Right, identifiers, unusableAsFalse)),
            OrNode o => Or(Build(o.Left, identifiers, unusableAsFalse), Build(o.Right, identifiers, unusableAsFalse)),
            _ => throw new ArgumentOutOfRangeException(nameof(node)),
        };
    }

    private static string Show(Occurrence<decimal>[][] file) => string.Join(", ", file.Select((bag, i) =>
        $"{Name(i)} = {(bag.Length == 0 ? "absent" : $"{{{string.Join(", ", bag.Select(o => o is Usable<decimal>(var d) ? $"{d}" : "bad"))}}}")}"));
}
