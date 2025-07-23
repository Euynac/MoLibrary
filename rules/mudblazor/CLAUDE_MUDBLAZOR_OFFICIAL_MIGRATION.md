
官方迁移文档：https://github.com/MudBlazor/MudBlazor/issues/9953
内容梗概：

Previous migration guide: v7.0.0 Migration Guide.

.NET 7 is no longer supported
For .NET 7 support, stay on the latest v7.x.x version. For bug fixes in v7, please PR and we'll release updates.

More details: #9511

Dialog
Caution

Change MudDialogInstance to IMudDialogInstance.

DialogOptions is now an immutable record. Use SetOptionsAsync and with to modify DialogOptions:

[CascadingParameter]
private IMudDialogInstance MudDialog { get; set; }

private Task ToggleFullscreenAsync()
{
    var options = MudDialog.Options with
    {
        FullScreen = !(MudDialog.Options.FullScreen ?? false)
    };

    return MudDialog.SetOptionsAsync(options);
}
More details: #10066

KeyInterceptor
IKeyInterceptor / KeyInterceptor, IKeyInterceptorFactory / KeyInterceptorFactory were removed and replaced with KeyInterceptorService. More details: #9896

KeyOptions is now immutable. More details: #9969

MudPopoverService
Removed from the core, replaced by PopoverService. The MudPopoverService was legacy, provided only for cases where you might encounter issues with PopoverService. If you encounter issues, please submit an issue.

More details: #9957

DialogService
Removed OnDialogInstanceAdded, use DialogInstanceAddedAsync instead. #9980
Converters
In Converter.cs, several property signatures have been updated to enhance localization support. These properties no longer return English messages directly. Instead, they now return keys intended for localization, accompanied by additional parameters:

OnError: Updated from Action<string>? to Action<string, object[]>?. This allows passing a key and optional parameters, enabling more flexible and accurate localization.
SetErrorMessage and GetErrorMessage: Both changed from string? to (string, object[])?. These properties now provide a key for localization along with relevant parameters, shifting message formatting to the localization system.
EventUtil
The following API has been removed:

EventUtil.AsNonRenderingEventHandler(...)
It has been replaced with:

this.AsNonRenderingEventHandler(...)
Because this is now an extension method that requires a reference to ComponentBase. The previous EventUtil.AsNonRenderingEventHandler did not propagate exceptions to ErrorBoundary (see dotnet/aspnetcore#54543). The new method ensures that exceptions are properly handled and propagated.

More details: #9967

Charts
TimeSeriesDiplayType was renamed to TimeSeriesDisplayType.
EndSlopeSpline, ILineInterpolator, Matrix, MatrixSolver, NaturalSpline, PeriodicSpline, SplineInterpolator, SvgCircle, SvgPath, SvgText are now internal.
TimeSeriesChartSeries, TimeSeriesDisplayType moved to MudBlazor namespace for consistency.
More details: #9919

DataGrid
CellActions:
The following properties are now "required":

SetSelectedItemAsync
StartEditingItemAsync
CancelEditingItemAsync
ToggleHierarchyVisibilityForItemAsync
Column:

Removed Action ColumnStateHasChanged
MudDataGrid:

_classname renamed to Classname
_style renamed to Stylename
_tableStyle renamed to TableStyle
_tableClass renamed to TableClass
_headClassname renamed to HeadClassname
_footClassname renamed to FootClassname
_headerFooterStyle renamed to HeaderFooterStyle
Removed CancelledEditingItem, replace with CanceledEditingItem. #9982
More details: #10149

MudTheme
BaseTypography is now an abstract class! If you used new BaseTypography() please replace it with corresponding implementation. #9434
Class Default was renamed to DefaultTypography to a allow source generation STJ usage. #9434
Class H1 was renamed to H1Typography. #9962
Class H2 was renamed to H2Typography. #9962
Class H3 was renamed to H3Typography. #9962
Class H4 was renamed to H4Typography. #9962
Class H5 was renamed to H5Typography. #9962
Class H6 was renamed to H6Typography. #9962
Class Subtitle1 was renamed to Subtitle1Typography. #9962
Class Subtitle2 was renamed to Subtitle2Typography. #9962
Class Body1 was renamed to Body1Typography. #9962
Class Body2 was renamed to Body2Typography. #9962
Class Input and the property was removed, use subtitle1. #10028
Class Button was renamed to ButtonTypography. #9962
Class Caption was renamed to CaptionTypography. #9962
Class Overline was renamed to OverlineTypography. #9962
In Typography the FontWeight and LineHeight are now an string type instead of int. #9011
DropZone
MudDropContainer: Removed GetTransactionOrignZoneIdentiifer, replace with GetTransactionOriginZoneIdentifier. #9981
MudDropContainer: Removed GetTransactionOrignZoneIdentifier, replace with GetTransactionOriginZoneIdentifier. #9981
MudDropContainer: Removed GetTransactionCurrentZoneIdentiifer, replace with GetTransactionCurrentZoneIdentifier. #9981
MudDropContainer: Removed IsOrign, replace with IsOrigin. #9981
Inputs
Typo.input was removed. Use Typo.subtitle1 instead. #10028
Accordingly all input typo CSS was removed. Use --mud-typography-subtitle1 instead. #10028
Note: This changes the order of the Typo enum to where it was before v7. #10028
MudInputAdornment.Edge was replaced with MudInputAdornment.Placement. #10057
Popover menus now originate from under the field by default. #10071
Autocomplete
No longer wraps around to the top item when using arrow keys in order to align with similar components. #10161
The public ScrollToListItemAsync method was removed. #10161
When Immediate is enabled, the value is now coerced immediately. #10138
MudColor
Renamed HslChanged to HslEquals, and the result is now not reverted. #10355
Equals now checks both RGBA and HSL values. Previously, it only checked RGBA. #10355
MudMenu
Menus can now be directly nested without additional setup. Nested menus inside another MudMenu will now be rendered as a MudMenuItem instead of MudButton. #10452, #10469
The popover now originates from under the activator by default. #10071
IconSize was removed in order to align with Material Design. #10478 #10542
MudChip
The MudChip now renders a semantic anchor tag in place if the div if an href is specified. It can now be a true anchor and so no longer acts like a button. The browser now handles the click (and Enter key) so OnClick is disabled and the close button is no longer shown (if applicable). This improves accessibility, including allowing the user to hover it to see the link it will direct them to, middle click to open in a new tab, or drag to another place.


Other Changes
MudTable: EditButtonContext namespace changed from MudBlazorFix to MudBlazor. #9952
MudCollapse: Reworked to use a grid instead of JavaScript for height calculation. Animation is now a static 300 ms. #10056
MudSelectItem: Ripple, Href, ForceLoad, and OnClick parameters removed. They were never implemented. #10045
MudBaseSelectItem: Removed base class. Copy-paste its code if you inherited this class. #10045
MudFormComponent: Dispose(bool disposing) replaced with protected virtual ValueTask DisposeAsyncCore(). This affects all components inheriting this base class. #10037
ResizeObserver: Now internal and doesn't implement IDisposable. Inject IResizeObserver / IResizeObserverFactory via DI to use its API. #10055
EventListener: Now internal and doesn't implement IDisposable. Inject IEventListener / IEventListenerFactory via DI to use its API. #10051
JsApiService: Now internal. Inject IJsApiService via DI to use its API. #9994
JsEvent: Now internal and doesn't implement IDisposable. Inject IJsEvent / IJsEventFactory via DI to use its API. #9996 #10061
ScrollListener: Now internal. Inject IScrollListener / IScrollListenerFactory via DI to use its API. #10048
ScrollSpy: Now internal. Inject IScrollSpy / IScrollSpyFactory via DI to use its API. #10048
ScrollManager: Now internal. Inject IScrollManager via DI to use its API. #10048
ScrollManagerException: Removed, not used in the core. #10048
ScrollOptions: Removed, not used in the core. #10048
Renamed IIJSRuntimeExtentions to IJSRuntimeExtensions. #9997
Renamed SortingAssistent to SortingAssistant. #9952
Removed EnableIllegalRazorParameterDetection from MudGlobal. #9580
MudCheckBox: Renamed HandleKeyDown to HandleKeyDownAsync. #9921
MudNumericField: Renamed HandleKeydown to HandleKeyDownAsync, HandleKeyUp to HandleKeyUpAsync, OnMouseWheel to OnMouseWheelAsync. #9921
MudSwitch: Renamed HandleKeyDown to HandleKeyDownAsync. #9921
MudFileUpload: Now generates a new input with every file change to retain references to previous files. It is no longer possible to supply your own id to the underlying input. More details: #9600
MudRadio, MudCheckBox, MudSwitch: LabelPosition and Placement replaced by LabelPlacement of type Placement (enum). Checked property replaced by T Value. #9472
MudSwitch: SwitchLabelClassname was renamed to LabelClassName. #9472
MudDrawer: The variant Temporary / Persistent has been changed to behave non-responsively, meaning solely controlled by the Open parameter. #10095
MudBreadcrumbs: BreadcrumbItem is now a record and any class that inherits from it must be updated to a record as well. #10116
MudBreadcrumbs: The ul element is now an ol wrapped in a nav in order to improve accessibility semantics. #10115
MudToggleItem: The span containing the checkmark icon was removed and the icon is now a direct child of mud-button-label. Checkmark classes that were on the span have been moved to the icon as well.
MudSelect: The dropdown is now opened on pointerdown instead of click to match the behavior of similar components. #10129
MudListItem: Parameter Gutters was made nullable. #10199
MudNumericField: Changed default InputMode to InputMode.decimal. #9923
MudPopover: RelativeWidth is now nullable bool?. #10238
MudSwipeArea: Now uses onpointer* instead of ontouch*. Now supports swipes with mouse as well. #9445
SnackbarOptions: The delegate invoked on Snackbar click has been renamed from Onclick to OnClick. #8589
MudSelect: Changed the visibility of _currentIcon from public to internal. #10451
MudOverlay: Moved to SectionOutlet with MudPopoverProvider in all cases except dialogs, absolute positioned overlays, and overlays with child content. Should have minimal impact unless you have previously used a workaround for overlay positioning. Unit tests now always need a popover provider if they want to interact/check if the mud-overlay exists,
i.e. var providerComp = Context.RenderComponent<MudPopoverProvider>(); #10446
MudOverlay: Now positioned statically and re positioned for nested popovers. to avoid this behavior set Absolute="true". #10446
MudToggleGroup: Removed Rounded in favor of our CSS utility classes, i.e. rounded-pill. #10533