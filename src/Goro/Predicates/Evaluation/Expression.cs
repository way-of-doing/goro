using Goro.Predicates.Values;

namespace Goro.Predicates.Evaluation;

/// <summary>
/// A checked, resolved expression. Evaluation trees are immutable and hold no per-file state, so one
/// tree serves every concurrent evaluation for the whole run.
/// </summary>
/// <remarks>
/// The non-generic base is what lowering works with, since it knows each node's type only as data;
/// the typed subclasses are what it builds. <see cref="Apply{TResult}"/> is the bridge between the two:
/// it hands the expression back to a generic method with its datum type as a type argument.
/// </remarks>
public abstract class Expression
{
    private protected Expression()
    {
    }

    public abstract GoroType Type { get; }

    /// <summary>Whether this expression has exactly one occurrence for every file.</summary>
    public abstract bool IsDefinite { get; }

    public abstract TResult Apply<TResult>(IExpressionFunc<TResult> func);
}

/// <summary>A computation that is generic over the datum type of the expression it is given.</summary>
public interface IExpressionFunc<out TResult>
{
    TResult Invoke<T>(Expression<T> expression) where T : notnull;
}

/// <summary>An expression whose values hold data of type <typeparamref name="T"/>.</summary>
public abstract class Expression<T> : Expression where T : notnull
{
    public sealed override GoroType Type => GoroTypes.Of<T>();

    public sealed override TResult Apply<TResult>(IExpressionFunc<TResult> func) => func.Invoke(this);

    public abstract Value<T> Evaluate(EvaluationContext context);
}
