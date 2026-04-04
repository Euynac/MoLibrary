using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Monica.Repository.Persistence.Services.Support;


public class CharTrimEndValueConverter() : ValueConverter<string, string>(x => x.TrimEnd(), x => x.TrimEnd());