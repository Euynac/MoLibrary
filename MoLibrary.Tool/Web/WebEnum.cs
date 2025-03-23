using System.ComponentModel;

namespace MoLibrary.Tool.Web;

/// <summary>
/// 常用ContentType
/// </summary>
public enum WebContentType
{
    /// <summary>
    /// application/x-www-form-urlencoded 即?key1=value1＆key2=value2形式，在发送前要自行编码key和value
    /// </summary>
    [Description("application/x-www-form-urlencoded")]
    General,
    /// <summary>
    /// application/json 是POST请求以JSON的格式向服务请求发起请求或者请求返回JSON格式的响应内容，服务端接受到数据后对JSON进行解析拿到所需要的参数。自行处理数据为{"title":"test","sub":[1,2,3]}这种格式
    /// </summary>
    [Description("application/json")]
    Json,
    /// <summary>
    /// text/plain	空格要转换为 "+" 加号，但不用对特殊字符编码。
    /// </summary>
    [Description("text/plain")]
    Plain,
    /// <summary>
    /// multipart/form-data 是使用POST请求上传文件，如果上传照片，文件等，不用对字符编码。
    /// </summary>
    [Description("multipart/form-data")]
    Upload,
    /// <summary>
    /// text/html
    /// </summary>
    [Description("text/html")]
    Html,
    /// <summary>
    /// application/octet-stream 上传二进制流数据
    /// </summary>
    [Description("application/octet-stream")]
    Stream,
}

/// <summary>
/// Method to send request.
/// </summary>
public enum HttpMethods
{
    /// <summary>
    /// 
    /// </summary>
    GET,
    /// <summary>
    /// 
    /// </summary>
    POST,
}

/// <summary>
/// Http supported chart set.
/// </summary>
public enum WebCharSet
{
    /// <summary>
    /// 世界通用语言编码
    /// </summary>
    [Description("charset=utf-8")] UTF8,

    /// <summary>
    /// 中文编码
    /// </summary>
    [Description("charset=gb2312")] GB2312,

    /// <summary>
    /// 繁体中文编码
    /// </summary>
    [Description("charset=big5")] BIG5,

    /// <summary>
    /// 西欧的编码，英文编码
    /// </summary>
    [Description("charset=iso-8859-1")] ISO88591,
}