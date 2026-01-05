using MudBlazor;

namespace MoLibrary.Framework.UI.UIRegisterCentre.Extensions;

/// <summary>
/// 状态显示信息的统一数据结构
/// </summary>
/// <param name="Color">MudBlazor Color 枚举值</param>
/// <param name="HexColor">十六进制颜色值，用于 CSS 样式</param>
/// <param name="Text">本地化显示文本</param>
/// <param name="Icon">MudBlazor 图标路径</param>
public record StatusDisplayInfo(
    Color Color,
    string HexColor,
    string Text,
    string Icon
);
