using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Tests.Predicates.Evaluation.Support;

/// <summary>A condition with a fixed truth that counts how often it was decided.</summary>
internal sealed class Probe(Truth truth) : Condition
{
    public int Decisions { get; private set; }

    public override Truth Decide(EvaluationContext context)
    {
        Decisions++;
        return truth;
    }

    public override string ToString() => truth.ToString();
}
