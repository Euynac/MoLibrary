using System.Security.Cryptography;
using System.Globalization;
using Monica.Tool.Security;
using Monica.Tool.Signing;

namespace Test.Monica.Tool.Security;

public sealed class SecurityContractTests
{
    [Theory]
    [InlineData("abc", "900150983cd24fb0d6963f7d28e17f72")]
    [InlineData("", "d41d8cd98f00b204e9800998ecf8427e")]
    public void ComputeHex_WhenUsingDefaultAlgorithm_ShouldReturnMd5(string input, string expected)
    {
        Hashing.ComputeHex(input).Should().Be(expected);
    }

    [Fact]
    public void ComputeHex_WhenUsingSha256AndUppercase_ShouldReturnExpectedText()
    {
        Hashing.ComputeHex("abc", HashAlgorithmName.SHA256, lowercase: false)
            .Should().Be("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD");
    }

    [Fact]
    public void Base64Codec_WhenTextIsValid_ShouldRoundTripUnicode()
    {
        const string value = "Monica 中文 😀";

        Base64Codec.Decode(Base64Codec.Encode(value)).Should().Be(value);
    }

    [Fact]
    public void Base64Codec_WhenInputIsMalformed_ShouldReturnOriginalText()
    {
        Base64Codec.Decode("not base64!").Should().Be("not base64!");
    }

    [Fact]
    public void BuildPlainText_WhenOptionsAreDefault_ShouldSortAndEncodeFields()
    {
        var model = new SignableModel { Zeta = "a value", Alpha = 42, Ignored = "secret" };

        var payload = ObjectSignatureBuilder.BuildPlainText(model);

        payload.Should().Be("alpha=42&zeta=a+value");
    }

    [Fact]
    public void BuildFieldMap_WhenOnlyMarkedPropertiesAreRequested_ShouldHonorAttribute()
    {
        var model = new SignableModel { Zeta = "value", Alpha = 42, Ignored = "secret" };
        var options = new SignatureOptions { UseSignatureOnlyAttributes = true };

        ObjectSignatureBuilder.BuildFieldMap(model, options)
            .Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, string?>(nameof(SignableModel.Alpha), "42"));
    }

    [Fact]
    public void BuildFieldMap_WhenFormattingNumbers_ShouldUseInvariantCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            var fields = ObjectSignatureBuilder.BuildFieldMap(new NumericModel { Amount = 1.5m }, ignoreNullValues: true);

            fields[nameof(NumericModel.Amount)].Should().Be("1.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Fact]
    public void BuildFieldMap_WhenObjectGraphContainsCycle_ShouldRejectIt()
    {
        var model = new CyclicModel();
        model.Next = model;

        var act = () => ObjectSignatureBuilder.BuildFieldMap(model, ignoreNullValues: true);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cycle*");
    }

    [Fact]
    public void BuildFieldMap_WhenFlattenedPropertiesCollide_ShouldRejectIt()
    {
        var model = new CollisionModel
        {
            First = new NestedModel { Value = "first" },
            Second = new NestedModel { Value = "second" }
        };

        var act = () => ObjectSignatureBuilder.BuildFieldMap(model, ignoreNullValues: true);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Value*");
    }

    [Fact]
    public void BuildHash_WhenRuntimeObjectContentChanges_ShouldChangeHash()
    {
        var first = ObjectSignatureBuilder.BuildHash(new PolymorphicModel { Payload = new NestedNumber { Amount = 1 } });
        var second = ObjectSignatureBuilder.BuildHash(new PolymorphicModel { Payload = new NestedNumber { Amount = 2 } });

        first.Should().NotBe(second);
    }

    [Fact]
    public void BuildHash_WhenCollectionContentChanges_ShouldChangeHash()
    {
        var first = ObjectSignatureBuilder.BuildHash(new CollectionModel { Values = [1, 2] });
        var second = ObjectSignatureBuilder.BuildHash(new CollectionModel { Values = [1, 3] });

        first.Should().NotBe(second);
    }

    [Fact]
    public void BuildHash_WhenDictionaryInsertionOrderChanges_ShouldRemainDeterministic()
    {
        var first = new DictionaryModel { Values = new Dictionary<string, int> { ["a"] = 1, ["b"] = 2 } };
        var second = new DictionaryModel { Values = new Dictionary<string, int> { ["b"] = 2, ["a"] = 1 } };

        ObjectSignatureBuilder.BuildHash(first).Should().Be(ObjectSignatureBuilder.BuildHash(second));
    }

    private sealed class SignableModel
    {
        public string? Zeta { get; init; }

        [SignatureOnly]
        public int Alpha { get; init; }

        [IgnoreSignature]
        public string? Ignored { get; init; }
    }

    private sealed class NumericModel
    {
        public decimal Amount { get; init; }
    }

    private sealed class CyclicModel
    {
        public CyclicModel? Next { get; set; }
    }

    private sealed class CollisionModel
    {
        public required NestedModel First { get; init; }
        public required NestedModel Second { get; init; }
    }

    private sealed class NestedModel
    {
        public required string Value { get; init; }
    }

    private sealed class PolymorphicModel
    {
        public required object Payload { get; init; }
    }

    private sealed class NestedNumber
    {
        public int Amount { get; init; }
    }

    private sealed class CollectionModel
    {
        public required List<int> Values { get; init; }
    }

    private sealed class DictionaryModel
    {
        public required Dictionary<string, int> Values { get; init; }
    }
}
