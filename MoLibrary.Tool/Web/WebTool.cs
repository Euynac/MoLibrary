using System;
using System.Security.Cryptography;
using System.Text;

namespace MoLibrary.Tool.Web
{
    /// <summary>
    /// 网络/安全方向工具
    /// </summary>
    public class WebTool
    {
        /// <summary>
        /// Base64加密，出错将返回原文，默认按UTF8加密
        /// </summary>
        /// <param name="source">原文</param>
        /// <param name="codeType">编码类型</param>
        /// <returns></returns>
        public static string EncodeBase64(string source, Encoding? codeType = null)
        {
            if (string.IsNullOrEmpty(source)) return "";
            codeType ??= Encoding.UTF8;
            string encode;
            try
            {
                var bytes = codeType.GetBytes(source);
                encode = Convert.ToBase64String(bytes);
            }
            catch
            {
                encode = source;
            }
            return encode;
        }

        /// <summary>
        /// Base64解密，出错将返回原文，默认按UTF8解密
        /// </summary>
        /// <param name="source">原文</param>
        /// <param name="codeType">编码类型，为空默认是UTF8</param>
        /// <returns></returns>
        public static string DecodeBase64(string source, Encoding? codeType = null)
        {
            if (string.IsNullOrEmpty(source)) return "";
            codeType ??= Encoding.UTF8;
            string decode;
            try
            {
                var bytes = Convert.FromBase64String(source);
                decode = codeType.GetString(bytes);
            }
            catch
            {
                decode = source;
            }
            return decode;
        }


        /// <summary>
        /// Compute string hash use specific hash algorithm.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="hashAlgorithm">Default is use MD5</param>
        /// <returns></returns>
        public static string StringHash(string str, HashAlgorithmName? hashAlgorithm = null)
        {
            if (string.IsNullOrEmpty(str)) return "";
            hashAlgorithm ??= HashAlgorithmName.MD5;
            var sb = new StringBuilder();
            using var hash = HashAlgorithm.Create(hashAlgorithm.ToString())!;
            var enc = Encoding.UTF8;
            var result = hash.ComputeHash(enc.GetBytes(str));
            foreach (var b in result)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Advance version in https://github.com/tmenier/Flurl</remarks>
        /// <param name="urlBase"></param>
        /// <param name="urlAppend"></param>
        /// <returns></returns>
        public static string Combine(string urlBase, string urlAppend) => $"{urlBase.TrimEnd('/')}/{urlAppend.TrimStart('/')}";
    }
}
