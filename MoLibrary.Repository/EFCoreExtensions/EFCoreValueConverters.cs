using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MoLibrary.Repository.EFCoreExtensions;


public class CharTrimEndValueConverter() : ValueConverter<string, string>(x => x.TrimEnd(), x => x.TrimEnd());