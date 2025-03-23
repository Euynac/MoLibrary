using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Web;
using MoLibrary.Tool.Extensions;
using MoLibrary.Tool.Web;

namespace MoLibrary.Tool.Interfaces
{
    /// <summary>
    /// 自动签名类中忽略签名的字段
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class NotSign : Attribute, ISignableModel { }
    /// <summary>
    /// 自动签名类中仅指定签名的字段（当Setting中开启仅签名才会生效，开启后忽略签名标签失效）
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class OnlySign : Attribute, ISignableModel { }
    public interface ISignableModel { }//仅用于标签约束
    /// <summary>
    /// 可签名模型接口（自动根据属性名及值ToString()（属性必为public）进行签名）
    /// </summary>
    public interface ISignableModel<T> : ISignableModel where T : class, ISignableModel<T>
    {
        /// <summary>
        /// 快速获得该接口对象。
        /// </summary>
        /// <returns></returns>
        public ISignableModel<T> GetSignableModel() => this;

        /// <summary>
        /// 当前自动签名设置
        /// </summary>
        public SignableModelSetting SignSetting => new();
        /// <summary>
        /// 修改自动签名设置
        /// </summary>
        /// <param name="settingAction"></param>
        /// <returns></returns>
        public ISignableModel<T> SetSetting(Action<SignableModelSetting> settingAction)
        {
            settingAction(SignSetting);
            return this;
        }
        /// <summary>
        /// 忽视指定字段不进行签名
        /// </summary>
        /// <returns></returns>
        public ISignableModel<T> Ignore(string ignoreName)
        {
            if (!SignSetting.IgnoreList.Contains(ignoreName)) SignSetting.IgnoreList.Add(ignoreName);
            return this;
        }
        /// <summary>
        /// 取消忽视指定字段不进行签名
        /// </summary>
        /// <returns></returns>
        public ISignableModel<T> UnIgnore(string ignoreName)
        {
            if (SignSetting.IgnoreList.Contains(ignoreName)) SignSetting.IgnoreList.Remove(ignoreName);
            return this;
        }
        /// <summary>
        /// 获取当前对象签名后的字符串
        /// </summary>
        /// <returns></returns>
        public string Sign() => Sign(null);

        /// <summary>
        /// 获取当前对象签名后的字符串
        /// </summary>
        /// <param name="supplement">需要补充到最后再进行签名的字符串</param>
        /// <returns></returns>
        public string Sign(string? supplement)
        {
            var result = WebTool.StringHash(GetConcatStr() + supplement);
            return SignSetting.NeedToLower ? result.ToLower() : result;
        }



        /// <summary>
        /// 获取拼接后但未加密的字符串
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
        /// ASCII比较器
        /// </summary>
        class AsciiCompare : IComparer<string>
        {
            public int Compare(string? x, string? y) => string.CompareOrdinal(x, y);

        }
        /// <summary>
        /// 根据当前签名设置获取当前对象对应的字典，不会返回null
        /// </summary>
        /// <returns></returns>
        public IDictionary<string, string?> GetDictionaryUseSetting() =>
            GetDictionary((T)this, typeof(T), SignSetting);


        /// <summary>
        /// 根据指定设置获取指定对象对应的字典，不会返回null
        /// </summary>
        /// <returns></returns>
        public static IDictionary<string, string?> GetDictionary(object instance, Type type, SignableModelSetting setting)
        {
            IDictionary<string, string?> propertyInfoDict = setting.NeedAsciiSort
                ? new SortedDictionary<string, string?>(new AsciiCompare())
                : new Dictionary<string, string?>();
            foreach (var propertyInfo in type.GetProperties())
            {
                //处理加了标签的
                if (!setting.EnableOnlySign)
                {
                    if (propertyInfo.GetCustomAttribute<NotSign>() != null) continue;
                }
                else
                {
                    if (propertyInfo.GetCustomAttribute<OnlySign>() == null) continue;
                }

                //获取属性值：
                var propertyValue = propertyInfo.GetValue(instance);
                if (propertyValue == null && setting.NotSignNull) continue;
                //获取属性名：
                var propertyName = propertyInfo.Name;
                if (setting.IgnoreList.Contains(propertyName) ||
                    propertyName == nameof(SignSetting)) continue;
                //获取属性类型，以支持嵌套
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
        /// 直接获取当前对象对应的字典
        /// </summary>
        /// <param name="ignoreNull">忽视null值，不添加到字典中</param>
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
    /// 签名方式
    /// </summary>
    public enum SignWay
    {
        /// <summary>
        /// 传统方式，即key1=value1&key2=value2 value将被urlEncode
        /// </summary>
        Traditional,
    }
    public class SignableModelSetting
    {
        /// <summary>
        /// 启用仅签名标签（即反转标签），打了Sign标签的才会签名，否则忽略
        /// </summary>
        public bool EnableOnlySign { get; set; }
        /// <summary>
        /// 空字段不签名，跳过
        /// </summary>
        public bool NotSignNull { get; set; } = true;
        /// <summary>
        /// 需要跳过的字段名（使用nameof，增加时增加一个使用add，多个使用addRange）
        /// </summary>
        public HashSet<string> IgnoreList { get; set; } = new();
        /// <summary>
        /// 需要字段按照Ascii码排序后进行签名
        /// </summary>
        public bool NeedAsciiSort { get; set; } = true;
        /// <summary>
        /// 签名结果需要转小写
        /// </summary>
        public bool NeedToLower { get; set; } = true;

        /// <summary>
        /// 拼接的字符串需要转小写
        /// </summary>
        public bool KeyToLower { get; set; } = true;
        /// <summary>
        /// 签名方式
        /// </summary>
        public SignWay Way { get; set; } = SignWay.Traditional;
    }
}