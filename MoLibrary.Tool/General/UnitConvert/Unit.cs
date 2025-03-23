using System;
using System.Collections.Generic;
using System.Linq;

namespace MoLibrary.Tool.General.UnitConvert;

public class UnitValue
{
    public Unit Unit { get; set; }
    public double Value { get; set; }

    public UnitValue(double value, Unit unit)
    {
        Value = value;
        Unit = unit;
    }
    public UnitValue ConvertToUnit(Unit toUnit)
    {
        
        return new UnitValue(toUnit.FromRootValue(Unit.ToRootValue(Value)), toUnit);
    }

    public override string ToString()
    {
        return $"{Value}{Unit.Symbol}";
    }
}
public class Unit
{
    public string Symbol { get; set; }
    public List<string> UnitNames { get; set; } 
    /// <summary>
    /// Factor to base.
    /// </summary>
    public double Factor { get; set; }
    /// <summary>
    /// Introduction of this unit.
    /// </summary>
    public string? Description { get; set; }

    public Unit ParentUnit { get; set; }
    public Func<double,double>? ParentToCurUnitFunc { get; set; }
    public Func<double, double>? CurToParentUnitFunc { get; }
    public bool IsRootLeafUnit => ParentToCurUnitFunc == null || CurToParentUnitFunc == null;
    public override string ToString() => Symbol;

    public Unit(string symbol, double factor, string? alias = null, Unit? parentUnit = null)
    {
        ParentUnit = parentUnit ?? this;
        Symbol = symbol;
        Factor = factor;
        UnitNames = new List<string> {Symbol};
        UnitNames.AddRange(alias?.Split('|').ToList() ?? new List<string>());
    }

    public Unit(Unit parentUnit, Func<double, double> parentToCurUnitFunc, string symbol, Func<double, double> curToParentUnitFunc, string? alias = null) : this(symbol, 0, alias, parentUnit)
    {
        ParentToCurUnitFunc = parentToCurUnitFunc;
        CurToParentUnitFunc = curToParentUnitFunc;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="parentUnit"></param>
    /// <param name="factor">one base unit value equal to how many current unit?</param>
    /// <param name="symbol"></param>
    /// <param name="alias"></param>
    public Unit(Unit parentUnit, double factor, string symbol, string? alias = null) :
        this(symbol, parentUnit.Factor / factor, alias, parentUnit)
    {
        
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="symbol"></param>
    /// <param name="factor">one current unit value equal to how many base unit?</param>
    /// <param name="parentUnit"></param>
    /// <param name="alias"></param>
    public Unit(string symbol, double factor, Unit parentUnit, string? alias = null) :
        this(symbol, parentUnit.Factor * factor, alias, parentUnit)
    {
       
    }

    public Unit GetRootUnit()
    {
        return ParentUnit == this ? this : ParentUnit.GetRootUnit();
    }

    /// <summary>
    /// From parent unit value to current unit value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public double FromRootValue(double value)
    {
        return IsRootLeafUnit ? value / Factor : ParentToCurUnitFunc!(ParentUnit.FromRootValue(value));
    }

    /// <summary>
    /// From current unit value to parent unit value.
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    public double ToRootValue(double value)
    {
        return IsRootLeafUnit ? value * Factor : ParentUnit.ToRootValue(CurToParentUnitFunc!(value));
    }

    public UnitValue GetUnitValue(double currentUnitValue) => new(currentUnitValue, this);
    public UnitValue ConvertToUnit(double currentUnitValue, Unit toUnit)
    {
        return GetUnitValue(currentUnitValue).ConvertToUnit(toUnit);
    } 
}