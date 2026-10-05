namespace Goro.Messages;

/// <summary>
/// Puts errors into words, in one language. The engine reports an <see cref="ErrorMessage"/> as
/// data, and only the edges of the program, where text is written for a person, render it.
/// </summary>
public interface IErrorMessages
{
    /// <summary>The message as a whole sentence, or more than one.</summary>
    string Render(ErrorMessage message);

    /// <summary>What the first line of an error starts with, before its message.</summary>
    string ErrorPrefix { get; }

    /// <summary>What the line of each suggested rewrite starts with, before the rewrite.</summary>
    string SuggestionLabel { get; }
}
