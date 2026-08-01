using Confluent.Kafka;
using Monica.EventBus.Kafka.Models;

namespace Monica.EventBus.Kafka.Providers.ConfluentKafka;

/// <summary>
/// Maps legacy ListGroups results to provider-neutral consumer-group descriptions.
/// </summary>
internal static class KafkaLegacyConsumerGroupMapper
{
    /// <summary>
    /// Creates the diagnostic returned when the broker omits a requested consumer group.
    /// </summary>
    public static KafkaConsumerGroupDescription CreateMissing(string groupId)
    {
        return new KafkaConsumerGroupDescription
        {
            GroupId = groupId,
            ErrorMessage = "The broker did not return a description for this consumer group."
        };
    }

    /// <summary>
    /// Maps one legacy group result and contains malformed assignment diagnostics to that group.
    /// </summary>
    public static KafkaConsumerGroupDescription Map(GroupInfo group)
    {
        ArgumentNullException.ThrowIfNull(group);
        var errorMessage = FormatError(group.Error);
        if (errorMessage is not null)
        {
            return CreateDiagnostic(group, errorMessage);
        }

        if (group.Members.Count > 0 &&
            !string.Equals(group.ProtocolType, "consumer", StringComparison.OrdinalIgnoreCase))
        {
            return CreateDiagnostic(
                group,
                $"Kafka group '{group.Group}' uses protocol type '{group.ProtocolType}', so its assignments cannot be decoded as consumer assignments.");
        }

        try
        {
            var members = group.Members
                .Select(MapMember)
                .OrderBy(member => member.ConsumerId, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return CreateDescription(group, null, members);
        }
        catch (InvalidDataException ex)
        {
            return CreateDiagnostic(
                group,
                $"Kafka consumer group '{group.Group}' returned an invalid member assignment: {ex.Message}");
        }
    }

    private static KafkaConsumerGroupDescription CreateDiagnostic(GroupInfo group, string errorMessage)
    {
        return CreateDescription(group, errorMessage, []);
    }

    private static KafkaConsumerGroupMemberAssignment MapMember(GroupMemberInfo member)
    {
        return new KafkaConsumerGroupMemberAssignment
        {
            ConsumerId = member.MemberId ?? string.Empty,
            Host = member.ClientHost ?? string.Empty,
            ClientId = member.ClientId ?? string.Empty,
            Partitions = KafkaConsumerProtocolAssignmentDecoder.Decode(member.MemberAssignment)
        };
    }

    private static KafkaConsumerGroupDescription CreateDescription(
        GroupInfo group,
        string? errorMessage,
        IReadOnlyList<KafkaConsumerGroupMemberAssignment> members)
    {
        return new KafkaConsumerGroupDescription
        {
            GroupId = group.Group ?? string.Empty,
            State = group.State ?? string.Empty,
            ErrorMessage = errorMessage,
            Members = members
        };
    }

    private static string? FormatError(Error error)
    {
        if (error.Code == ErrorCode.NoError)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(error.Reason)
            ? error.Code.ToString()
            : $"{error.Code}: {error.Reason}";
    }
}
