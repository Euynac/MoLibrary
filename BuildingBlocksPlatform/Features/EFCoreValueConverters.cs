using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BuildingBlocksPlatform.Features;


public class CharTrimEndValueConverter() : ValueConverter<string, string>(x => x.TrimEnd(), x => x.TrimEnd());