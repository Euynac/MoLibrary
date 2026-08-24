using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Monica.Tool.Extensions;

namespace Test.Monica.Tool.Extensions;

public sealed class ExtensionContractTests
{
    [Fact]
    public async Task StreamHelpers_WhenReadingText_ShouldPreservePositionAndOwnership()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Monica"));
        stream.Position = 3;

        (await stream.ReadAsAsStringWithoutChangePosAsync()).Should().Be("Monica");
        stream.Position.Should().Be(3);

        stream.ConvertToString().Should().Be("ica");
        stream.CanRead.Should().BeTrue();
    }

    [Fact]
    public async Task CreateMemoryStreamAsync_WhenCopyFails_ShouldResetSeekableSource()
    {
        using var stream = new CancelingSeekableStream { Position = 4 };
        Func<Task> copy = async () => await stream.CreateMemoryStreamAsync();

        await copy.Should().ThrowAsync<OperationCanceledException>();
        stream.Position.Should().Be(0);
    }

    [Fact]
    public void EnumerableHelpers_WhenSourceIsSingleUse_ShouldEnumerateOnlyOnce()
    {
        var source = new SingleUseEnumerable<KeyValuePair<int, string?>>(
            [new(1, null), new(2, "value")]);

        source.TryGetKey(null, out var key).Should().BeTrue();
        key.Should().Be(1);
    }

    [Fact]
    public void IsNullOrEmptySet_WhenEnumeratorIsDisposable_ShouldDisposeIt()
    {
        var source = new DisposableEnumerable();

        ((IEnumerable)source).IsNullOrEmptySet().Should().BeFalse();
        source.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public void TakeTuple_WhenSourceIsUnbounded_ShouldStopAtLastRequestedIndex()
    {
        var result = Infinite().TakeTuple(2, 4);

        result.Should().Be((2, 4));
    }

    [Fact]
    public void TimeHelpers_WhenFindingNextBoundaries_ShouldReturnPositiveAccurateSpans()
    {
        var value = new DateTime(2026, 8, 13, 10, 15, 30, 500, DateTimeKind.Utc);

        value.NextMinuteSpan().Should().Be(TimeSpan.FromSeconds(29.5));
        value.NextHourSpan().Should().Be(TimeSpan.FromMinutes(44) + TimeSpan.FromSeconds(29.5));
        value.NextDaySpan().Should().Be(TimeSpan.FromHours(13) + TimeSpan.FromMinutes(44) + TimeSpan.FromSeconds(29.5));
        TimeExtensions.Truncate(value, TimeExtensions.DateTimePart.Year)
            .Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        TimeExtensions.Truncate(value, TimeExtensions.DateTimePart.Month)
            .Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void EnumFlags_WhenUnderlyingTypeIsUnsignedLong_ShouldPreserveHighBits()
    {
        var combined = LargeFlags.Low.Add(LargeFlags.High);

        combined.Should().Be(LargeFlags.Low | LargeFlags.High);
        combined.Remove(LargeFlags.Low).Should().Be(LargeFlags.High);
        "Low,High".RetrieveFlags<LargeFlags>(',').Should().Be(combined);
    }

    [Fact]
    public void JsonPreview_WhenPositionUsesUtf8Bytes_ShouldMarkExactCharacterBoundary()
    {
        JsonExceptionExtensions.NormalizeCount("éx", 1).Should().Be(0);
        JsonExceptionExtensions.NormalizeCount("éx", 2).Should().Be(1);
        JsonExceptionExtensions.GetPreviewAroundBytesPosition("éx", 2)
            .Should().Be("é<<< ERROR HERE <<<x");
    }

    [Fact]
    public void PopulateObject_WhenReferencePropertyReceivesArray_ShouldReplaceIt()
    {
        var target = new JsonModel { Values = [1] };

        JsonSerializerExtensions.PopulateObject(target, "{\"Values\":[2,3],\"ReadOnly\":9}");

        target.Values.Should().Equal(2, 3);
        target.ReadOnly.Should().Be(7);
    }

    [Fact]
    public void PopulateObject_WhenDictionaryReceivesObjectJson_ShouldReplaceItsEntries()
    {
        var target = new JsonModel { Map = new Dictionary<string, int> { ["old"] = 1 } };

        JsonSerializerExtensions.PopulateObject(target, "{\"Map\":{\"new\":2}}");

        target.Map.Should().ContainSingle().Which.Should().Be(new KeyValuePair<string, int>("new", 2));
    }

    [Fact]
    public void AttributeCache_WhenAttributeIsMissing_ShouldCacheNegativeLookupSafely()
    {
        typeof(AttributeModel).GetCustomAttributeCached<DescriptionAttribute>().Should().BeNull();
        typeof(AttributeModel).GetCustomAttributeCached<DescriptionAttribute>().Should().BeNull();
        typeof(AttributeModel).GetCustomAttributeCached<DescriptionAttribute, AttributeModel, object>(
            model => model.Value).Should().BeNull();
    }

    [Fact]
    public void AttributeCache_WhenSourceTypeIsCollectible_ShouldNotRootItsAssembly()
    {
        var weakType = CacheCollectibleType();

        for (var attempt = 0; attempt < 10 && weakType.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        weakType.IsAlive.Should().BeFalse();
    }

    [Fact]
    public void StringHelpers_WhenCharactersAreRegexMetacharacters_ShouldTreatThemLiterally()
    {
        "a]b-c^d\\e".FilterChars(']', '-', '^', '\\').Should().Be("abcde");
        "ab".Repeat(3).Should().Be("ababab");
        "abc".AllIndexOf(string.Empty).Should().BeEmpty();
    }

    private static IEnumerable<int> Infinite()
    {
        for (var value = 0;; value++) yield return value;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CacheCollectibleType()
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"Monica.Tool.Tests.Collectible.{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.RunAndCollect);
        var module = assembly.DefineDynamicModule("Main");
        var type = module.DefineType("CollectibleModel").CreateType()!;
        type.GetCustomAttributeCached<DescriptionAttribute>().Should().BeNull();
        return new WeakReference(type);
    }

    [Flags]
    private enum LargeFlags : ulong
    {
        Low = 1,
        High = 1UL << 63
    }

    private sealed class JsonModel
    {
        public int[] Values { get; set; } = [];
        public Dictionary<string, int> Map { get; set; } = [];
        public int ReadOnly => 7;
    }

    private sealed class CancelingSeekableStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => 8;
        public override long Position { get; set; }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            Position = Math.Min(Length, Position + 2);
            throw new OperationCanceledException();
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Position = Math.Min(Length, Position + 2);
            throw new OperationCanceledException(cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length + offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            return Position;
        }

        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class AttributeModel
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class SingleUseEnumerable<T>(IEnumerable<T> values) : IEnumerable<T>
    {
        private bool _enumerated;

        public IEnumerator<T> GetEnumerator()
        {
            if (_enumerated) throw new InvalidOperationException("The source was enumerated more than once.");
            _enumerated = true;
            return values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class DisposableEnumerable : IEnumerable
    {
        public bool WasDisposed { get; private set; }

        public IEnumerator GetEnumerator() => new Enumerator(this);

        private sealed class Enumerator(DisposableEnumerable owner) : IEnumerator, IDisposable
        {
            private bool _moved;
            public object Current => 1;

            public bool MoveNext()
            {
                if (_moved) return false;
                _moved = true;
                return true;
            }

            public void Reset() => _moved = false;
            public void Dispose() => owner.WasDisposed = true;
        }
    }
}
