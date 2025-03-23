using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Tool.General.UnitConvert;

public abstract class UnitConverterBase
{
    public List<Unit> Units { get; set; }
    public Dictionary<string, Unit> UnitDictionary { get; set; }
    public string Regex { get; set; }
    public delegate bool ValueStrConverter(string input, out double num);
    private ValueStrConverter _valueStrConverter;
    /// <summary>
    /// 
    /// </summary>
    /// <param name="unitFactory">quick regex: var (.+?) .*\n?</param>
    /// <param name="nameIgnoreCase"></param>
    protected UnitConverterBase(Func<List<Unit>> unitFactory, bool nameIgnoreCase = true)
    {
        Units = unitFactory.Invoke();
        var tmp = Units.SelectMany(unit => unit.UnitNames.Select(name => new KeyValuePair<string, Unit>(name, unit)));
        UnitDictionary = nameIgnoreCase ? tmp.ToIgnoreCaseDictionary() : tmp.ToDictionary();
        _valueStrConverter = double.TryParse;
        Regex = $"({UnitDictionary.Keys.StringJoin('|')})$";
    }

    public void SetCustomValueStrConverter(ValueStrConverter converter)
    {
        _valueStrConverter = converter;
    }

    public bool ContainsCurUnit(string str) => str.IsMatch(Regex, RegexOptions.IgnoreCase);
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
        if (!strWithUnit.MatchOnceThenReplace(Regex, out var strValuePart, out var matched, RegexOptions.IgnoreCase)) return false;
        if (!_valueStrConverter(strValuePart, out fromUnitValue)) return false;
        fromUnit = UnitDictionary[matched[0].Value];
        return true;
    }
    public bool TryGetUnit(string strWithUnit, [MaybeNullWhen(false)] out Unit fromUnit, out string strValuePart)
    {
        fromUnit = null;
        if (!strWithUnit.MatchOnceThenReplace(Regex, out strValuePart, out var matched, RegexOptions.IgnoreCase))
        {
            return false;
        }

        fromUnit = UnitDictionary[matched[0].Value];
        return true;
    }

    public UnitValue? Convert(string fromUnitStr, string toUnitName)
    {

        if (!TryGetUnit(fromUnitStr, out var fromUnit, out double fromUnitValue))
            return null;

        if (!TryGetUnit(toUnitName, out var toUnit)) throw new InvalidOperationException($"not support unit name {toUnitName}");
        return fromUnit.GetUnitValue(fromUnitValue).ConvertToUnit(toUnit);
    }
}