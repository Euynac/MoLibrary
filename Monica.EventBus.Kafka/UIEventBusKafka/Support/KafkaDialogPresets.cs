using MudBlazor;

namespace Monica.EventBus.Kafka.UIEventBusKafka.Support;

/// <summary>
/// Creates the consistent dialog layouts used by the Kafka console.
/// </summary>
internal static class KafkaDialogPresets
{
    /// <summary>
    /// Gets a full-width option set for standard editor and confirmation dialogs.
    /// </summary>
    public static DialogOptions Standard => Create(MaxWidth.Medium);

    /// <summary>
    /// Gets a full-width option set for message-preview dialogs.
    /// </summary>
    public static DialogOptions Message => Create(MaxWidth.Large);

    /// <summary>
    /// Gets a full-width option set for evidence-rich detail dialogs.
    /// </summary>
    public static DialogOptions Detail => Create(MaxWidth.ExtraExtraLarge);

    private static DialogOptions Create(MaxWidth maxWidth) => new()
    {
        CloseOnEscapeKey = true,
        MaxWidth = maxWidth,
        FullWidth = true
    };
}
