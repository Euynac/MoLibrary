using System.ComponentModel;

namespace Monica.Tool.Web;

/// <summary>
/// Common ContentType
/// </summary>
public enum HttpContentType
{
    /// <summary>
    /// application/x-www-form-urlencoded is the form of ?key1=value1&amp;key2=value2. You must encode
    /// the key and value yourself before sending.
    /// </summary>
    [Description("application/x-www-form-urlencoded")]
    General,
    /// <summary>
    /// application/json is a POST request that initiates a request to the service in JSON format or requests to return the response content in JSON format. After receiving the data, the server parses the JSON to obtain the required parameters. Process the data yourself into the format {"title":"test","sub":[1,2,3]}
    /// </summary>
    [Description("application/json")]
    Json,
    /// <summary>
    /// Text/plain spaces are converted to "+" plus signs, but special characters are not encoded.
    /// </summary>
    [Description("text/plain")]
    Plain,
    /// <summary>
    /// multipart/form-data uses POST requests to upload files. If you upload photos, files, etc., there is no need to encode characters.
    /// </summary>
    [Description("multipart/form-data")]
    Upload,
    /// <summary>
    /// text/html
    /// </summary>
    [Description("text/html")]
    Html,
    /// <summary>
    /// application/octet-stream uploads binary stream data
    /// </summary>
    [Description("application/octet-stream")]
    Stream,
}

/// <summary>
/// Method to send request.
/// </summary>
public enum HttpMethodKind
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
public enum HttpCharset
{
    /// <summary>
    /// universal language encoding
    /// </summary>
    [Description("charset=utf-8")] UTF8,

    /// <summary>
    /// Chinese encoding
    /// </summary>
    [Description("charset=gb2312")] GB2312,

    /// <summary>
    /// Traditional Chinese encoding
    /// </summary>
    [Description("charset=big5")] BIG5,

    /// <summary>
    /// Western European encoding, English encoding
    /// </summary>
    [Description("charset=iso-8859-1")] ISO88591,
}
