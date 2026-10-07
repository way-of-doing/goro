using Goro.Predicates.Values;

namespace Goro.Tests.Predicates.Values;

public class GoroTypesTests
{
    [Test]
    public void Of_MapsEveryDatumType()
    {
        Assert.Multiple(() =>
        {
            Assert.That(GoroTypes.Of<string>(), Is.EqualTo(GoroType.String));
            Assert.That(GoroTypes.Of<decimal>(), Is.EqualTo(GoroType.Number));
            Assert.That(GoroTypes.Of<ByteCount>(), Is.EqualTo(GoroType.ByteCount));
            Assert.That(GoroTypes.Of<Duration>(), Is.EqualTo(GoroType.Duration));
            Assert.That(GoroTypes.Of<bool>(), Is.EqualTo(GoroType.Boolean));
            Assert.That(GoroTypes.Of<Blob>(), Is.EqualTo(GoroType.Blob));
        });
    }

    [Test]
    public void Of_AnyOtherType_Throws()
    {
        Assert.Throws<TypeInitializationException>(() => GoroTypes.Of<int>());
    }

    [Test]
    public void Name_IsTheLanguagesOwnSpellingOfEachType()
    {
        Assert.That(Enum.GetValues<GoroType>().Select(GoroTypes.Name),
            Is.EqualTo(new[] { "string", "number", "bytecount", "duration", "boolean", "blob" }));
    }
}
