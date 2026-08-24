using System.Reflection;
using System.Runtime.InteropServices;
using Confluent.Kafka;

namespace Monica.EventBus.Kafka.Providers.ConfluentKafka;

/// <summary>
/// Reads legacy consumer-group snapshots while guaranteeing native result ownership.
/// </summary>
/// <remarks>
/// The Confluent.Kafka legacy group-list path does not guarantee release of the group-list
/// pointer when librdkafka returns a partial result. This isolated compatibility reader preserves
/// the protocol path needed to avoid coordinator-targeted Admin crashes and releases every
/// returned pointer in a <see langword="finally"/> block.
/// </remarks>
internal static class ConfluentKafkaLegacyGroupReader
{
    private const string LIBRDKAFKA_TYPE_NAME = "Confluent.Kafka.Impl.Librdkafka";
    private const int MAX_GROUP_COUNT = 100_000;
    private const int MAX_MEMBER_COUNT = 100_000;
    private const int MAX_MEMBER_PAYLOAD_BYTES = 16 * 1024 * 1024;
    private const long MAX_TOTAL_MEMBER_PAYLOAD_BYTES = 64L * 1024 * 1024;

    private static readonly Type LIBRDKAFKA_TYPE = ResolveLibrdkafkaType();
    private static readonly PropertyInfo LIBRDKAFKA_HANDLE_PROPERTY = ResolveLibrdkafkaHandleProperty();
    private static readonly NativeListGroups LIST_GROUPS = ResolveLibrdkafkaMethod("list_groups")
        .CreateDelegate<NativeListGroups>();
    private static readonly Action<IntPtr> DESTROY_GROUP_LIST = ResolveLibrdkafkaMethod("group_list_destroy")
        .CreateDelegate<Action<IntPtr>>();

    internal delegate ErrorCode NativeListGroups(
        IntPtr clientHandle,
        string? groupId,
        out IntPtr groupList,
        IntPtr timeoutMilliseconds);

    internal static List<GroupInfo> ListGroups(
        IAdminClient admin,
        int timeoutMilliseconds,
        IReadOnlySet<string>? includedGroupIds,
        bool includeMemberPayloads)
    {
        ArgumentNullException.ThrowIfNull(admin);
        var safeHandle = GetLibrdkafkaHandle(admin);
        return ExecuteNativeRequest(
            safeHandle,
            timeoutMilliseconds,
            includedGroupIds,
            includeMemberPayloads,
            LIST_GROUPS,
            DESTROY_GROUP_LIST);
    }

    internal static List<GroupInfo> ExecuteNativeRequest(
        SafeHandle clientHandle,
        int timeoutMilliseconds,
        IReadOnlySet<string>? includedGroupIds,
        bool includeMemberPayloads,
        NativeListGroups listGroups,
        Action<IntPtr> destroyGroupList)
    {
        ArgumentNullException.ThrowIfNull(clientHandle);
        ArgumentNullException.ThrowIfNull(listGroups);
        ArgumentNullException.ThrowIfNull(destroyGroupList);
        if (timeoutMilliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeoutMilliseconds),
                "Kafka group-list timeout must be greater than zero.");
        }

        var handleReferenceAdded = false;
        var groupListPointer = IntPtr.Zero;
        try
        {
            clientHandle.DangerousAddRef(ref handleReferenceAdded);
            var errorCode = listGroups(
                clientHandle.DangerousGetHandle(),
                groupId: null,
                out groupListPointer,
                (IntPtr)timeoutMilliseconds);
            if (errorCode != ErrorCode.NoError)
            {
                throw new KafkaException(errorCode);
            }

            if (groupListPointer == IntPtr.Zero)
            {
                throw new InvalidDataException("librdkafka returned a successful group-list result without a result pointer.");
            }

            return CopyGroupList(groupListPointer, includedGroupIds, includeMemberPayloads);
        }
        finally
        {
            try
            {
                if (groupListPointer != IntPtr.Zero)
                {
                    destroyGroupList(groupListPointer);
                }
            }
            finally
            {
                if (handleReferenceAdded)
                {
                    clientHandle.DangerousRelease();
                }
            }
        }
    }

    private static List<GroupInfo> CopyGroupList(
        IntPtr groupListPointer,
        IReadOnlySet<string>? includedGroupIds,
        bool includeMemberPayloads)
    {
        var nativeList = Marshal.PtrToStructure<NativeGroupList>(groupListPointer);
        ValidateCount(nativeList.GroupCount, MAX_GROUP_COUNT, "group count");
        if (nativeList.GroupCount > 0 && nativeList.Groups == IntPtr.Zero)
        {
            throw new InvalidDataException("librdkafka returned a group count without a group array.");
        }

        var groups = new List<GroupInfo>(includedGroupIds?.Count ?? nativeList.GroupCount);
        long totalMemberPayloadBytes = 0;
        for (var groupIndex = 0; groupIndex < nativeList.GroupCount; groupIndex++)
        {
            var nativeGroup = ReadElement<NativeGroupInfo>(nativeList.Groups, groupIndex);
            var groupId = ReadString(nativeGroup.Group);
            if (includedGroupIds is not null && !includedGroupIds.Contains(groupId))
            {
                continue;
            }

            groups.Add(CopyGroup(nativeGroup, groupId, includeMemberPayloads, ref totalMemberPayloadBytes));
        }

        return groups;
    }

    private static GroupInfo CopyGroup(
        NativeGroupInfo nativeGroup,
        string groupId,
        bool includeMemberPayloads,
        ref long totalMemberPayloadBytes)
    {
        ValidateCount(nativeGroup.MemberCount, MAX_MEMBER_COUNT, $"member count for group '{groupId}'");
        if (nativeGroup.MemberCount > 0 && nativeGroup.Members == IntPtr.Zero)
        {
            throw new InvalidDataException($"librdkafka returned members for group '{groupId}' without a member array.");
        }

        var members = new List<GroupMemberInfo>(nativeGroup.MemberCount);
        for (var memberIndex = 0; memberIndex < nativeGroup.MemberCount; memberIndex++)
        {
            var nativeMember = ReadElement<NativeGroupMemberInfo>(nativeGroup.Members, memberIndex);
            members.Add(new GroupMemberInfo(
                ReadString(nativeMember.MemberId),
                ReadString(nativeMember.ClientId),
                ReadString(nativeMember.ClientHost),
                includeMemberPayloads
                    ? CopyMemberPayload(
                        nativeMember.MemberMetadata,
                        nativeMember.MemberMetadataSize,
                        "member metadata",
                        ref totalMemberPayloadBytes)
                    : [],
                includeMemberPayloads
                    ? CopyMemberPayload(
                        nativeMember.MemberAssignment,
                        nativeMember.MemberAssignmentSize,
                        "member assignment",
                        ref totalMemberPayloadBytes)
                    : []));
        }

        return new GroupInfo(
            new BrokerMetadata(
                nativeGroup.Broker.Id,
                ReadString(nativeGroup.Broker.Host),
                nativeGroup.Broker.Port),
            groupId,
            new Error(nativeGroup.ErrorCode),
            ReadString(nativeGroup.State),
            ReadString(nativeGroup.ProtocolType),
            ReadString(nativeGroup.Protocol),
            members);
    }

    private static byte[] CopyMemberPayload(
        IntPtr payload,
        IntPtr payloadSize,
        string fieldName,
        ref long totalMemberPayloadBytes)
    {
        var byteLength = payloadSize.ToInt64();
        if (byteLength < 0 || byteLength > MAX_MEMBER_PAYLOAD_BYTES)
        {
            throw new InvalidDataException(
                $"librdkafka returned {fieldName} length {byteLength}, outside the supported safety limit.");
        }

        if (byteLength == 0)
        {
            return [];
        }

        if (payload == IntPtr.Zero)
        {
            throw new InvalidDataException($"librdkafka returned {fieldName} bytes without a payload pointer.");
        }

        totalMemberPayloadBytes = checked(totalMemberPayloadBytes + byteLength);
        if (totalMemberPayloadBytes > MAX_TOTAL_MEMBER_PAYLOAD_BYTES)
        {
            throw new InvalidDataException(
                $"librdkafka consumer-group payloads exceed the {MAX_TOTAL_MEMBER_PAYLOAD_BYTES} byte safety limit.");
        }

        var result = GC.AllocateUninitializedArray<byte>(checked((int)byteLength));
        Marshal.Copy(payload, result, 0, result.Length);
        return result;
    }

    private static T ReadElement<T>(IntPtr arrayPointer, int index) where T : struct
    {
        var byteOffset = checked(index * Marshal.SizeOf<T>());
        return Marshal.PtrToStructure<T>(IntPtr.Add(arrayPointer, byteOffset));
    }

    private static string ReadString(IntPtr value)
    {
        return value == IntPtr.Zero
            ? string.Empty
            : Marshal.PtrToStringUTF8(value) ?? string.Empty;
    }

    private static void ValidateCount(int count, int maximumCount, string fieldName)
    {
        if (count < 0 || count > maximumCount)
        {
            throw new InvalidDataException(
                $"librdkafka returned {fieldName} {count}, outside the supported safety limit.");
        }
    }

    private static SafeHandle GetLibrdkafkaHandle(IAdminClient admin)
    {
        return LIBRDKAFKA_HANDLE_PROPERTY.GetValue(admin.Handle) as SafeHandle
            ?? throw new InvalidOperationException("Confluent.Kafka did not expose the expected librdkafka safe handle.");
    }

    private static Type ResolveLibrdkafkaType()
    {
        return typeof(IAdminClient).Assembly.GetType(LIBRDKAFKA_TYPE_NAME, throwOnError: false)
            ?? throw new InvalidOperationException(
                $"Confluent.Kafka does not contain the expected compatibility type '{LIBRDKAFKA_TYPE_NAME}'.");
    }

    private static PropertyInfo ResolveLibrdkafkaHandleProperty()
    {
        return typeof(Handle).GetProperty("LibrdkafkaHandle", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Confluent.Kafka does not expose the expected internal librdkafka handle property.");
    }

    private static MethodInfo ResolveLibrdkafkaMethod(string methodName)
    {
        return LIBRDKAFKA_TYPE.GetMethod(methodName, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                $"Confluent.Kafka does not expose the expected compatibility method '{methodName}'.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeBrokerMetadata
    {
        public readonly int Id;
        public readonly IntPtr Host;
        public readonly int Port;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeGroupMemberInfo
    {
        public readonly IntPtr MemberId;
        public readonly IntPtr ClientId;
        public readonly IntPtr ClientHost;
        public readonly IntPtr MemberMetadata;
        public readonly IntPtr MemberMetadataSize;
        public readonly IntPtr MemberAssignment;
        public readonly IntPtr MemberAssignmentSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeGroupInfo
    {
        public readonly NativeBrokerMetadata Broker;
        public readonly IntPtr Group;
        public readonly ErrorCode ErrorCode;
        public readonly IntPtr State;
        public readonly IntPtr ProtocolType;
        public readonly IntPtr Protocol;
        public readonly IntPtr Members;
        public readonly int MemberCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeGroupList
    {
        public readonly IntPtr Groups;
        public readonly int GroupCount;
    }
}
