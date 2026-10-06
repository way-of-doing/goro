// Owned by the evaluator group (G5) of the predicate-runtime-architecture line.
using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>An evaluated operand of a comparison, range or regex operator, with its quantifier.</summary>
internal readonly record struct OperatorOperand<T>(Value<T> Value, Quantifier Quantifier) where T : notnull;

/// <summary>
/// What a comparison, range or regex operator asks of one combination of its operands' data. An
/// operator implements it as a struct, so that the iteration is specialized for it.
/// </summary>
internal interface IOperatorCondition<T> where T : notnull
{
    /// <summary>Prepares one datum before it is tested; applied once to each usable occurrence.</summary>
    T Prepare(T datum);

    /// <summary>Whether the condition holds of one combination of prepared data, in written order.</summary>
    bool HoldsOf(ReadOnlySpan<T> data);
}

/// <summary>
/// How every comparison, range and regex operator turns its operands into a truth, exactly as
/// docs/concepts/evaluation.md writes it: false if any operand is absent; otherwise nested
/// iteration over the operands' occurrences, universals outermost and written order breaking
/// ties, testing every combination and combining the outcomes by each operand's quantifier.
/// </summary>
internal static class OperatorEvaluation
{
    /// <param name="operands">Every operand, already evaluated, in written order.</param>
    public static Truth Decide<T, TCondition>(
        EvaluationContext context, ReadOnlySpan<OperatorOperand<T>> operands, TCondition condition)
        where T : notnull
        where TCondition : IOperatorCondition<T>
    {
        // An absent operand makes the operator false before any iteration, which is also what keeps
        // universal quantification from being vacuous.
        foreach (var operand in operands)
        {
            if (operand.Value.IsAbsent)
            {
                return Truth.False;
            }
        }

        var combinations = new Combinations<T, TCondition>(
            context, operands, condition, stackalloc int[operands.Length], stackalloc int[operands.Length]);
        return combinations.Over(0);
    }

    /// <summary>
    /// The state of one operator's iteration. The methods are named for the functions of the
    /// specification they implement.
    /// </summary>
    private ref struct Combinations<T, TCondition>
        where T : notnull
        where TCondition : IOperatorCondition<T>
    {
        private readonly EvaluationContext context;
        private readonly ReadOnlySpan<OperatorOperand<T>> operands;
        private readonly TCondition condition;

        /// <summary>The operands' positions in nesting order: universals first, then existentials.</summary>
        private readonly Span<int> nesting;

        /// <summary>For each operand, by written position, the index of the occurrence chosen for it.</summary>
        private readonly Span<int> chosen;

        /// <summary>
        /// Every operand's prepared data, operand after operand, followed by room for one combination.
        /// An unusable occurrence has no datum, and its slot is left empty.
        /// </summary>
        private readonly T[] data;

        public Combinations(
            EvaluationContext context,
            ReadOnlySpan<OperatorOperand<T>> operands,
            TCondition condition,
            Span<int> nesting,
            Span<int> chosen)
        {
            this.context = context;
            this.operands = operands;
            this.condition = condition;
            this.nesting = nesting;
            this.chosen = chosen;

            var next = 0;
            foreach (var quantifier in (ReadOnlySpan<Quantifier>)[Quantifier.Universal, Quantifier.Existential])
            {
                for (var position = 0; position < operands.Length; position++)
                {
                    if (operands[position].Quantifier == quantifier)
                    {
                        nesting[next++] = position;
                    }
                }
            }

            // Each datum is prepared once here, rather than once for every combination it is in.
            var total = 0;
            foreach (var operand in operands)
            {
                total += operand.Value.Occurrences.Length;
            }

            data = new T[total + operands.Length];
            var slot = 0;
            foreach (var operand in operands)
            {
                foreach (var occurrence in operand.Value.Occurrences)
                {
                    if (occurrence is Usable<T>(var datum))
                    {
                        data[slot] = condition.Prepare(datum);
                    }

                    slot++;
                }
            }
        }

        /// <summary>
        /// Iterates the operand at nesting depth <paramref name="depth"/> over every one of its
        /// occurrences, without stopping once the result is settled, and quantifies the outcomes.
        /// </summary>
        public readonly Truth Over(int depth)
        {
            if (depth == nesting.Length)
            {
                return Test();
            }

            var position = nesting[depth];
            var operand = operands[position];
            var outcomes = new Quantification(operand.Quantifier);
            for (var index = 0; index < operand.Value.Occurrences.Length; index++)
            {
                chosen[position] = index;
                outcomes.Add(Over(depth + 1));
            }

            return outcomes.Result;
        }

        /// <summary>
        /// Tests one combination: unusable, after reporting each unusable occurrence in it, or the
        /// truth of the operator's condition of its data.
        /// </summary>
        private readonly Truth Test()
        {
            var combination = data.AsSpan(data.Length - operands.Length);
            var answerable = true;
            var firstSlot = 0;
            for (var position = 0; position < operands.Length; position++)
            {
                var occurrences = operands[position].Value.Occurrences;
                var index = chosen[position];
                if (occurrences[index] is Unusable<T> unusable)
                {
                    answerable = false;
                    if (unusable.Origin is { } origin)
                    {
                        context.Report(origin);
                    }
                }
                else
                {
                    combination[position] = data[firstSlot + index];
                }

                firstSlot += occurrences.Length;
            }

            return answerable ? Truths.Of(condition.HoldsOf(combination)) : Truth.Unusable;
        }
    }
}

/// <summary>
/// The outcomes of one operand's occurrences, combined by its quantifier with Kleene's rules: an
/// existential is an <c>OR</c> over the occurrences, and a universal an <c>AND</c>.
/// </summary>
internal struct Quantification(Quantifier quantifier)
{
    private bool anyTrue;
    private bool anyFalse;
    private bool anyUnusable;

    public void Add(Truth outcome)
    {
        switch (outcome)
        {
            case Truth.True:
                anyTrue = true;
                break;
            case Truth.False:
                anyFalse = true;
                break;
            default:
                anyUnusable = true;
                break;
        }
    }

    public readonly Truth Result => quantifier switch
    {
        Quantifier.Existential => anyTrue ? Truth.True : anyUnusable ? Truth.Unusable : Truth.False,
        _ => anyFalse ? Truth.False : anyUnusable ? Truth.Unusable : Truth.True,
    };
}
