using System.Collections.Immutable;
using System.Reflection;
using Goro.Messages;
using Goro.Predicates.Values;

namespace Goro.Tests.Messages;

/// <summary>
/// The rules every error message keeps, checked over every case there is: a message carries data,
/// never words, and the English provider has a whole sentence for every case and every value of
/// every situation it carries.
/// </summary>
public class ErrorMessageTests
{
    private static readonly Type[] Cases = typeof(ErrorMessage)
        .GetNestedTypes()
        .Where(type => type.IsSubclassOf(typeof(ErrorMessage)))
        .ToArray();

    private static IEnumerable<TestCaseData> EveryCase() =>
        Cases.Select(type => new TestCaseData(type).SetArgDisplayNames(type.Name));

    [Test]
    public void TheCases_AreThere()
    {
        Assert.That(Cases, Has.Length.GreaterThan(80));
    }

    [TestCaseSource(nameof(EveryCase))]
    public void ACase_IsSealed(Type type)
    {
        Assert.That(type.IsSealed, Is.True);
    }

    [TestCaseSource(nameof(EveryCase))]
    public void ACase_CarriesOnlyData(Type type)
    {
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Assert.Multiple(() =>
        {
            foreach (var property in properties)
            {
                Assert.That(property.PropertyType, Is.Not.EqualTo(typeof(string)), property.Name);
                Assert.That(IsData(property.PropertyType), Is.True, $"{property.Name} is a {property.PropertyType.Name}");
            }
        });
    }

    [TestCaseSource(nameof(EveryCase))]
    public void ACase_RendersInEnglish_ForEveryValueOfEverySituation_AsSentencesEndingWithGoro(Type type)
    {
        var rendered = Samples(type).Select(EnglishErrorMessages.Instance.Render).ToList();

        Assert.That(rendered, Is.Not.Empty);
        Assert.That(rendered, Has.All.EndsWith(", goro!"));
        Assert.That(rendered, Has.None.Match(@"\{[A-Za-z_]\w*\}"), "a placeholder left unfilled");
    }

    [Test]
    public void AnInvalidPattern_EndsTheEnginesReasonWithGoro_InPlaceOfItsPeriod()
    {
        var message = new ErrorMessage.InvalidPattern(new Code("r\"a(\""), new ForeignText("Not enough )'s."));

        Assert.That(EnglishErrorMessages.Instance.Render(message), Is.EqualTo("That pattern won't parse -- Not enough )'s, goro!"));
    }

    [Test]
    public void ATypeName_IsGorosOwnSpelling()
    {
        var message = new ErrorMessage.TypeMismatch(new Code("a"), GoroType.ByteCount, new Code("b"), GoroType.Boolean, new Code("=="));

        Assert.That(EnglishErrorMessages.Instance.Render(message), Is.EqualTo("`bytecount` and `boolean` don't compare, goro!"));
    }

    [Test]
    public void AListOfValidValues_IsJoinedWithCommas()
    {
        var message = new ErrorMessage.InvalidOutputFormat(new Code("yaml"), [new Code("plain"), new Code("json")]);

        Assert.That(EnglishErrorMessages.Instance.Render(message), Is.EqualTo("No format `yaml` -- pick from plain, json, goro!"));
    }

    [TestCase(1, "`COUNT()` takes one argument, not 2, goro!")]
    [TestCase(2, "`COUNT()` takes two arguments, not 2, goro!")]
    public void AnArgumentCount_HasASentenceForEachNumberAFunctionTakes(int expected, string sentence)
    {
        var message = new ErrorMessage.WrongArgumentCount(new Code("COUNT"), expected, 2);

        Assert.That(EnglishErrorMessages.Instance.Render(message), Is.EqualTo(sentence));
    }

    [Test]
    public void TheWordsOfLayout_AreTheProvidersOwn()
    {
        Assert.That(EnglishErrorMessages.Instance.ErrorPrefix, Is.EqualTo("goro: error: "));
        Assert.That(EnglishErrorMessages.Instance.SuggestionLabel, Is.EqualTo("try: "));
    }

    // ---------------------------------------------------------------------------------------------

    private static bool IsData(Type type) =>
        type == typeof(Code)
        || type == typeof(ForeignText)
        || type == typeof(GoroType)
        || type == typeof(int)
        || type == typeof(ImmutableArray<Code>)
        || (type.IsEnum && type.Namespace == typeof(ErrorMessage).Namespace);

    /// <summary>The case built once for every combination of the values each of its parameters can take.</summary>
    private static IEnumerable<ErrorMessage> Samples(Type type)
    {
        var constructor = PrimaryConstructor(type);
        var choices = constructor.GetParameters().Select(parameter => SampleValues(parameter.ParameterType)).ToList();

        IEnumerable<object?[]> combinations = [[]];
        foreach (var values in choices)
        {
            combinations = combinations.SelectMany(prefix => values.Select(value => (object?[])[.. prefix, value]));
        }

        return combinations.Select(arguments => (ErrorMessage)constructor.Invoke(arguments));
    }

    // A record's positional constructor, or its parameterless one; never the copy constructor.
    private static ConstructorInfo PrimaryConstructor(Type type) =>
        type.GetConstructors().Single(constructor => constructor.GetParameters() is not [{ } only] || only.ParameterType != type);

    private static object?[] SampleValues(Type type) => type switch
    {
        _ when type == typeof(Code) => [new Code("x")],
        _ when type == typeof(ForeignText) => [new ForeignText("Something is off.")],
        _ when type == typeof(int) => [0, 1, 2, 3],
        _ when type == typeof(ImmutableArray<Code>) => [ImmutableArray.Create(new Code("a"), new Code("b"))],
        _ when type.IsEnum => Enum.GetValues(type).Cast<object?>().ToArray(),
        _ => throw new NotSupportedException($"No samples of {type.Name}."),
    };
}
