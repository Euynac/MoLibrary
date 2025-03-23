using System.Collections.Generic;

namespace MoLibrary.Tool.General.UnitConvert;

public class LengthUnitConverter : UnitConverterBase
{
    public LengthUnitConverter() : base(() =>
    {
        var km = new Unit("km", 1, "千米|公里");
        var m = new Unit(km, 1000, "m", "米");
        var dm = new Unit(km, 10000, "dm", "分米");
        var cm = new Unit(m, 100, "cm", "厘米");
        var mm = new Unit(cm, 10, "mm", "毫米");
        var um = new Unit(mm, 1000, "um", "微米");
        var nm = new Unit(um, 1000, "nm", "纳米");
        var pm = new Unit(nm, 1000, "pm", "皮米");
        var 里 = new Unit(km, 2, "里");
        var 丈 = new Unit(m, 0.3, "丈");
        var 尺 = new Unit(丈, 10, "尺");
        var 寸 = new Unit(尺, 10, "寸");
        var 分 = new Unit(寸, 10, "分");
        var 厘 = new Unit(分, 10, "厘");
        var 毫 = new Unit(厘, 10, "毫");
        var 海里 = new Unit("nmi", 1.852, km, "海里");
        var fm = new Unit("fm", 1.8288, m, "英寻|fathom") { Description = "海洋测量中的深度单位" };
        var fur = new Unit("fur", 201.168, m, "弗隆|furlong");
        var yard = new Unit("yd", 0.9144, m, "yard|码");
        var foot = new Unit("ft", 0.3048, m, "foot|英尺");
        var mile = new Unit("mile", 5280, foot, "英里");
        var inch = new Unit("in", 2.54, cm, "inch|英寸");
        var ly = new Unit("ly", 9460730472580800, m, "光年");
        var au = new Unit("A.U.", 149597870700, m, "天文单位");
        return new List<Unit> { km, m, dm, cm, mm, um, nm, pm, 里, 丈, 尺, 寸, 分, 厘, 毫, 海里, fm, fur, yard, foot, mile, inch, ly, au, };
    })
    {

    }

}