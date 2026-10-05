using Goro.Predicates.Evaluation;
using Goro.Predicates.Values;

namespace Goro.Predicates.Binding;

/// <summary>
/// A checked, resolved expression. Bound trees are immutable and hold no per-file state, so one
/// tree serves every concurrent evaluation for the whole run.
/// </summary>
/// <remarks>
/// The non-generic base is what the binder works with while it is still discovering types; the
/// typed subclasses are what it builds. <see cref="Apply{TResult}"/> is the bridge between the two:
/// it hands the expression back to a generic method with its datum type as a type argument.
/// </remarks>
public abstract class BoundExpression
{
    private protected BoundExpression()
    {
    }

    public abstract GoroType Type { get; }

    /// <summary>Whether this expression has exactly one occurrence for every file.</summary>
    public abstract bool IsDefinite { get; }

    public abstract TResult Apply<TResult>(IBoundExpressionFunc<TResult> func);
}

/// <summary>A computation that is generic over the datum type of the expression it is given.</summary>
public interface IBoundExpressionFunc<out TResult>
{
    TResult Invoke<T>(BoundExpression<T> expression) where T : notnull;
}

/// <summary>An expression whose values hold data of type <typeparamref name="T"/>.</summary>
public abstract class BoundExpression<T> : BoundExpression where T : notnull
{
    public sealed override GoroType Type => GoroTypes.Of<T>();

    public sealed override TResult Apply<TResult>(IBoundExpressionFunc<TResult> func) => func.Invoke(this);

    public abstract Value<T> Evaluate(EvaluationContext context);
}
