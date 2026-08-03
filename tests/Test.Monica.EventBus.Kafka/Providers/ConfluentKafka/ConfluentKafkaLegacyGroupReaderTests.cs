using System.Runtime.InteropServices;
using AwesomeAssertions;
using Confluent.Kafka;
using Monica.EventBus.Kafka.Providers.ConfluentKafka;
using Monica.EventBus.Kafka.Services.Support;
using Xunit;

namespace Test.Monica.EventBus.Kafka.Providers.ConfluentKafka;

public class ConfluentKafkaLegacyGroupReaderTests
{
    [Fact]
    public void ExecuteNativeRequest_WhenLibrdkafkaReturnsPartial_ShouldReleaseReturnedGroupList()
    {
        using var clientHandle = new TestSafeHandle();
        var groupListPointer = new IntPtr(42);
        var destroyedPointer = IntPtr.Zero;

        var act = () => ConfluentKafkaLegacyGroupReader.ExecuteNativeRequest(
            clientHandle,
            timeoutMilliseconds: 1_000,
            includedGroupIds: null,
            includeMemberPayloads: true,
            ReturnPartial,
            pointer => destroyedPointer = pointer);

        act.Should().Throw<KafkaException>()
            .Which.Error.Code.Should().Be(ErrorCode.Local_Partial);
        destroyedPointer.Should().Be(groupListPointer);
        return;

        ErrorCode ReturnPartial(
            IntPtr handle,
            string? groupId,
            out IntPtr groupList,
            IntPtr timeoutMilliseconds)
        {
            groupList = groupListPointer;
            return ErrorCode.Local_Partial;
        }
    }

    [Fact]
    public void ExecuteNativeRequest_WhenLibrdkafkaReturnsEmptySuccess_ShouldCopyAndReleaseGroupList()
    {
        using var clientHandle = new TestSafeHandle();
        var groupListPointer = Marshal.AllocHGlobal(Marshal.SizeOf<TestNativeGroupList>());
        Marshal.StructureToPtr(
            new TestNativeGroupList
            {
                Groups = IntPtr.Zero,
                GroupCount = 0
            },
            groupListPointer,
            fDeleteOld: false);
        var wasReleased = false;

        try
        {
            var result = ConfluentKafkaLegacyGroupReader.ExecuteNativeRequest(
                clientHandle,
                timeoutMilliseconds: 1_000,
                includedGroupIds: null,
                includeMemberPayloads: true,
                ReturnSuccess,
                pointer =>
                {
                    Marshal.FreeHGlobal(pointer);
                    wasReleased = true;
                });

            result.Should().BeEmpty();
            wasReleased.Should().BeTrue();
        }
        finally
        {
            if (!wasReleased)
            {
                Marshal.FreeHGlobal(groupListPointer);
            }
        }

        return;

        ErrorCode ReturnSuccess(
            IntPtr handle,
            string? groupId,
            out IntPtr groupList,
            IntPtr timeoutMilliseconds)
        {
            groupList = groupListPointer;
            return ErrorCode.NoError;
        }
    }

    [Theory]
    [InlineData(-500, 1_000)]
    [InlineData(0, 1_000)]
    [InlineData(999, 1_000)]
    [InlineData(1_001, 1_001)]
    public void NormalizeTimeoutMilliseconds_ShouldEnforcePositiveNativeTimeout(
        int configuredMilliseconds,
        int expectedMilliseconds)
    {
        var result = KafkaClientConfigFactory.NormalizeTimeoutMilliseconds(
            TimeSpan.FromMilliseconds(configuredMilliseconds));

        result.Should().Be(expectedMilliseconds);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TestNativeGroupList
    {
        public IntPtr Groups;
        public int GroupCount;
    }

    private sealed class TestSafeHandle() : SafeHandle(IntPtr.Zero, ownsHandle: true)
    {
        public override bool IsInvalid => false;

        protected override bool ReleaseHandle()
        {
            return true;
        }
    }
}
