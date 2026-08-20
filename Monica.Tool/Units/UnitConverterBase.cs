using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using Monica.Tool.Extensions;

namespace Monica.Tool.Units;

public abstract class UnitConverterBase
{
    private readonly RegexOptions _regexOptions;
    private System.Text.RegularExpressions.Regex _unitSuffixRegex = null!;
    private string _regex = string.Empty;

    public List<Unit> Units { get; set; }
    public Dictionary<string, Unit> UnitDictionary { get; set; }
    public string Regex
    {
        get => _regex;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            _regex = value;
            _unitSuffixRegex = new System.Text.RegularExpressions.Regex(
                value,
                _regexOptions,
                TimeSpan.FromSeconds(1));
        }
    }
    public delegate bool ValueStrConverter(string input, out double num);
    private ValueStrConverter _valueStrConverter;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="unitFactory">quick regex: var (.+?) .*\n?</param>
    /// <param name="nameIgnoreCase"></param>
    protected UnitConverterBase(Func<List<Unit>> unitFactory, bool nameIgnoreCase = true)
    {
        ArgumentNullException.ThrowIfNull(unitFactory);

        Units = unitFactory.Invoke();
        foreach (var unit in Units)
        {
            unit.DimensionType = GetType();
        }

        var tmp = Units.SelectMany(unit => unit.UnitNames.Select(name => new KeyValuePair<string, Unit>(name, unit)));
        UnitDictionary = nameIgnoreCase ? tmp.ToIgnoreCaseDictionary() : tmp.ToDictionary();
        _valueStrConverter = double.TryParse;
        _regexOptions = nameIgnoreCase
            ? RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
            : RegexOptions.CultureInvariant;
        var alternatives = UnitDictionary.Keys
            .OrderByDescending(name => name.Length)
            .Select(System.Text.RegularExpressions.Regex.Escape);
        Regex = $"(?<unit>{alternatives.StringJoin('|')})$";
    }

    public void SetCustomValueStrConverter(ValueStrConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _valueStrConverter = converter;
    }

    public bool ContainsCurUnit(string str)
    {
        ArgumentNullException.ThrowIfNull(str);
        return _unitSuffixRegex.IsMatch(str);
    }
    public bool TryGetUnitValues(string strWithUnit, [MaybeNullWhen(false)] out List<UnitValue> values)
    {
        values = null;
        if (!TryGetUnit(strWithUnit, out var fromUnit, out double value)) return false;
        var fromUnitValue = fromUnit.GetUnitValue(value);
        values = Units.Select(unit => fromUnitValue.ConvertToUnit(unit)).ToList();
        return true;
    }

    public bool TryGetUnit(string unitName, [MaybeNullWhen(false)] out Unit unit) =>
        UnitDictionary.TryGetValue(unitName, out unit);
    public bool TryGetUnit(string strWithUnit, [MaybeNullWhen(false)] out Unit fromUnit, out double fromUnitValue)
    {
        fromUnit = null;
        fromUnitValue = 0;
        if (strWithUnit is null)
        {
            return false;
        }

        var matched = _unitSuffixRegex.Match(strWithUnit);
        if (!matched.Success)
        {
            return false;
        }

        var strValuePart = strWithUnit[..matched.Index];
        if (!_valueStrConverter(strValuePart, out fromUnitValue)) return false;
        return UnitDictionary.TryGetValue(GetMatchedUnitName(matched), out fromUnit);
    }
    public bool TryGetUnit(string strWithUnit, [MaybeNullWhen(false)] out Unit fromUnit, out string strValuePart)
    {
        fromUnit = null;
        strValuePart = string.Empty;
        if (strWithUnit is null)
        {
            return false;
        }

        var matched = _unitSuffixRegex.Match(strWithUnit);
        if (!matched.Success)
        {
            return false;
        }

        strValuePart = strWithUnit[..matched.Index];
        return UnitDictionary.TryGetValue(GetMatchedUnitName(matched), out fromUnit);
    }

    public UnitValue? Convert(string fromUnitStr, string toUnitName)
    {

        if (!TryGetUnit(fromUnitStr, out var fromUnit, out double fromUnitValue))
            return null;

        if (!TryGetUnit(toUnitName, out var toUnit)) throw new InvalidOperationException($"not support unit name {toUnitName}");
        return fromUnit.GetUnitValue(fromUnitValue).ConvertToUnit(toUnit);
    }

    private static string GetMatchedUnitName(Match match)
    {
        var namedGroup = match.Groups["unit"];
        return namedGroup.Success ? namedGroup.Value : match.Value;
    }
}
