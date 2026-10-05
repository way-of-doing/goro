namespace Goro.Predicates.Text;

/// <summary>How an operator prepares the strings it compares; see docs/concepts/normalization.md.</summary>
public enum ComparisonMode
{
    Normalized,
    Literal,
}
