using System.Collections.Generic;

namespace MoLibrary.Tool.General.UnitConvert;

public class TemperatureUnitConverter : UnitConverterBase
{
    public TemperatureUnitConverter() : base(() =>
    {
        var celsius = new Unit("℃", 1, "摄氏度");
        var fahrenheit = new Unit(celsius, c => c * 1.8 + 32, "℉", f => (f - 32) / 1.8, "华氏度");
        var t = new Unit(celsius, c => c + 273.15, "K", t => t - 273.15, "开尔文|开氏度");
        var rankine = new Unit(t, k => k * 1.8, "°R", r => r / 1.8, "兰式度");
        var reaumur = new Unit(celsius, c => c / 1.25, "°Re", re => re * 1.25, "列氏度");

        return new List<Unit> {celsius, fahrenheit, t, rankine, reaumur};
    })
    {

    }

}