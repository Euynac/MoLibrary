using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Monica.Repository.EFCoreExtensions;


public class CharTrimEndValueConverter() : ValueConverter<string, string>(x => x.TrimEnd(), x => x.TrimEnd());