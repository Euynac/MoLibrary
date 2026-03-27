using System.Reflection;
using System.Text;
using System.Web;
using Monica.Tool.Extensions;
using Monica.Tool.Web;

namespace Monica.Tool.Interfaces;

/// <summary>
/// Ignore signed fields in automatic signature class
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class NotSign : Attribute, ISignableModel { }
/// <summary>
/// Only the signature fields are specified in the automatic signature class (only the signature will take effect when it is turned on in Settings, and the signature tag will be ignored after it is turned on).
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class OnlySign : Attribute, ISignableModel { }
public interface ISignableModel { }//仅用于标签约束
/// <summary>
/// Signable model interface (automatically signed based on the attribute name and value ToString() (the attribute must be public))
/// </summary>
public interface ISignableModel<T> : ISignableModel where T : class, ISignableModel<T>
{
    /// <summary>
    /// Quickly obtain the interface object.
    /// </summary>
    /// <returns></returns>
    public ISignableModel<T> GetSignableModel() => this;

    /// <summary>
    /// Current auto-signing settings
    /// </summary>
    public SignableModelSetting SignSetting => new();
    /// <summary>
    /// Modify automatic signature settings
    /// </summary>
    /// <param name="settingAction"></param>
    /// <returns></returns>
    public ISignableModel<T> SetSetting(Action<SignableModelSetting> settingAction)
    {
        settingAction(SignSetting);
        return this;
    }
    /// <summary>
    /// Ignore specified fields and do not sign
    /// </summary>
    /// <returns></returns>
    public ISignableModel<T> Ignore(string ignoreName)
    {
        SignSetting.IgnoreList.Add(ignoreName);
        return this;
    }
    /// <summary>
    /// Cancel ignore the specified field and do not sign
    /// </summary>
    /// <returns></returns>
    public ISignableModel<T> UnIgnore(string ignoreName)
    {
        SignSetting.IgnoreList.Remove(ignoreName);
        return this;
    }
    /// <summary>
    /// Get the string after the signature of the current object
    /// </summary>
    /// <returns></returns>
    public string Sign() => Sign(null);

    /// <summary>
    /// Get the string after the signature of the current object
    /// </summary>
    /// <param name="supplement">String that needs to be added to the end before signing</param>
    /// <returns></returns>
    public string Sign(string? supplement)
    {
        var result = WebTool.StringHash(GetConcatStr() + supplement);
        return SignSetting.NeedToLower ? result.ToLower() : result;
    }



    /// <summary>
    /// Get the concatenated but unencrypted string
    /// </summary>
    /// <returns></returns>
    public string GetConcatStr()
    {
        var dict = GetDictionaryUseSetting();
        StringBuilder stringBuilder = new();
        switch (SignSetting.Way)
        {
            case SignWay.Traditional:
                foreach (var (key, value) in dict)
                {
                    stringBuilder.Append($"{SignSetting.KeyToLower.IIf(key.ToLower(), key)}={HttpUtility.UrlEncode(value)}&");
                }

                return stringBuilder.ToString().TrimEnd('&');//应该不会有参数会多个&吧
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    /// <summary>
    /// ASCII comparator
    /// </summary>
    class AsciiCompare : IComparer<string>
    {
        public int Compare(string? x, string? y) => string.CompareOrdinal(x, y);

    }
    /// <summary>
    /// Get the dictionary corresponding to the current object based on the current signature settings, and will not return null.
    /// </summary>
    /// <returns></returns>
    public IDictionary<string, string?> GetDictionaryUseSetting() =>
        GetDictionary((T)this, typeof(T), SignSetting);


    /// <summary>
    /// Get the dictionary corresponding to the specified object according to the specified settings, and will not return null.
    /// </summary>
    /// <returns></returns>
    public static IDictionary<string, string?> GetDictionary(object instance, Type type, SignableModelSetting setting)
    {
        IDictionary<string, string?> propertyInfoDict = setting.NeedAsciiSort
            ? new SortedDictionary<string, string?>(new AsciiCompare())
            : new Dictionary<string, string?>();
        foreach (var propertyInfo in type.GetProperties())
        {
            //Handle tagged
            if (!setting.EnableOnlySign)
            {
                if (propertyInfo.GetCustomAttribute<NotSign>() != null) continue;
            }
            else
            {
                if (propertyInfo.GetCustomAttribute<OnlySign>() == null) continue;
            }

            //Get attribute value:
            var propertyValue = propertyInfo.GetValue(instance);
            if (propertyValue == null && setting.NotSignNull) continue;
            //Get attribute name:
            var propertyName = propertyInfo.Name;
            if (setting.IgnoreList.Contains(propertyName) ||
                propertyName == nameof(SignSetting)) continue;
            //Gets the attribute type to support nesting
            var propertyType = propertyInfo.PropertyType;
            if (propertyValue != null && propertyType.IsClass && propertyType != typeof(string))
            {
                propertyInfoDict.AddRange(GetDictionary(propertyValue, propertyType, setting));
            }
            else propertyInfoDict.Add(propertyName, propertyValue?.ToString());
        }
        return propertyInfoDict;
    }
    /// <summary>
    /// Directly obtain the dictionary corresponding to the current object
    /// </summary>
    /// <param name="ignoreNull">Ignore null values ​​and do not add them to the dictionary</param>
    /// <returns></returns>
    public Dictionary<string, string?> GetDictionary(bool ignoreNull = true)
    {
        return (Dictionary<string, string?>)GetDictionary((T)this, typeof(T), new SignableModelSetting
        {
            NotSignNull = !ignoreNull
        });
    }

}
/// <summary>
/// Signature method
/// </summary>
public enum SignWay
{
    /// <summary>
    /// Traditional way, that is, key1=value1&amp;key2=value2 value will be urlEncoded
    /// </summary>
    Traditional,
}
public class SignableModelSetting
{
    /// <summary>
    /// Enable signature-only tags (ie reverse tags). Only those with the Sign tag will sign, otherwise they will be ignored.
    /// </summary>
    public bool EnableOnlySign { get; set; }
    /// <summary>
    /// Empty fields are not signed and skipped
    /// </summary>
    public bool NotSignNull { get; set; } = true;
    /// <summary>
    /// The name of the field that needs to be skipped (use nameof, add one to add, use addRange to add multiple)
    /// </summary>
    public HashSet<string> IgnoreList { get; set; } = new();
    /// <summary>
    /// The fields need to be sorted according to the Ascii code before signing.
    /// </summary>
    public bool NeedAsciiSort { get; set; } = true;
    /// <summary>
    /// The signature result needs to be converted to lowercase
    /// </summary>
    public bool NeedToLower { get; set; } = true;

    /// <summary>
    /// The concatenated strings need to be converted to lowercase
    /// </summary>
    public bool KeyToLower { get; set; } = true;
    /// <summary>
    /// Signature method
    /// </summary>
    public SignWay Way { get; set; } = SignWay.Traditional;
}