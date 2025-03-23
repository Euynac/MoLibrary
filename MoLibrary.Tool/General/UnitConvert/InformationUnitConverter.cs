using System.Collections.Generic;

namespace MoLibrary.Tool.General.UnitConvert;

public class InformationUnitConverter : UnitConverterBase
{
    public InformationUnitConverter() : base(() =>
    {
        var bit = new Unit("bit", 1, "比特");
        var @byte = new Unit("bytes", 8, bit, "字节");
        //国际单位制（SI）decimal
        var kb = new Unit("kB", 1000, @byte, "千字节|kilobyte");
        var mb = new Unit("MB", 1000, kb, "兆|MegaByte");
        var gb = new Unit("GB", 1000, mb, "GigaByte");
        var tb = new Unit("TB", 1000, gb, "TeraByte");
        var pb = new Unit("PB", 1000, tb, "PetaByte");
        var eb = new Unit("EB", 1000, pb, "ExaByte");
        var zb = new Unit("ZB", 1000, eb, "ZettaByte");
        var yb = new Unit("YB", 1000, zb, "YottaByte");
        var rb = new Unit("RB", 1000, zb, "Ronnabyte");
        var qb = new Unit("QB", 1000, zb, "Quettabyte");

        //International Electrotechnical Commission (IEC) binary.
        var kib = new Unit("KiB", 1024, @byte, "kibibyte");
        var mib = new Unit("MiB", 1024, kib, "mebibyte");
        var gib = new Unit("GiB", 1024, mib, "gibibyte");
        var tib = new Unit("TiB", 1024, gib, "tebibyte");
        var pib = new Unit("PiB", 1024, tib, "pebibyte");
        var eib = new Unit("EiB", 1024, pib, "exbibyte");
        var zib = new Unit("ZiB", 1024, eib, "zebibyte");
        var yib = new Unit("YiB", 1024, zib, "yobibyte");
        return new List<Unit> {bit, @byte, kb, mb, gb, tb, kib, mib, gib, tib, pb, eb, zb, yb,rb,qb, pib, eib, zib, yib};
    })
    {
        
    }

}