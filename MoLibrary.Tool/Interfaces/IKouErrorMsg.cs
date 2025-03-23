using System;
using JetBrains.Annotations;
using MoLibrary.Tool.Extensions;

namespace MoLibrary.Tool.Interfaces
{
    /// <summary>
    /// Kou环境下的错误信息接口（指示该类具有错误信息提供）
    /// </summary>
    public interface IKouErrorMsg
    {
        /// <summary>
        /// 错误信息
        /// </summary>
        public string? ErrorMsg { get; set; }
    }
    /// <summary>
    /// 可获得错误信息的接口
    /// </summary>
    public interface IKouError<T> : IKouErrorMsg where T : Enum
    {
        /// <summary>
        /// 错误代码
        /// </summary>
        public T ErrorCode { get; set; }
    }

    /// <summary>
    /// IKouErrorMsg接口的拓展方法（当升级到.net standard 2.1后可迁移到接口默认实现）
    /// </summary>
    public static class KouErrorMsgExtension
    {
        /// <summary>
        /// 快速打包错误（需要实现<see cref="IKouErrorMsg"/>接口）
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="errorMsg">错误内容</param>
        /// <returns>必定返回false用于快速return</returns>
        //[ContractAnnotation("=>false")]
        public static bool ReturnError(this IKouErrorMsg? obj, string? errorMsg = null)
        {
            if (obj == null) return false;
            obj.ErrorMsg = errorMsg;
            return false;
        }
        /// <summary>
        /// 检查是否有错误
        /// </summary>
        /// <param name="errorObject"></param>
        /// <returns></returns>
        public static bool HasError(this IKouErrorMsg errorObject) => !string.IsNullOrWhiteSpace(errorObject.ErrorMsg);
        #region 具体类型
        /// <summary>
        /// 移除发生的错误
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static void RemoveError<T>(this IKouError<T> obj) where T : struct,Enum
        {
            obj.ErrorCode = default;
            obj.ErrorMsg = null;
        }

        /// <summary>
        /// 继承某个实现了IKouError接口的类的错误
        /// </summary>
        /// <param name="inheritor">继承者</param>
        /// <param name="decedent">被继承者</param>
        /// <param name="supplement">补充，比如错误来源</param>
        /// <returns>必定返回false用于快速return (注意没有发现错误信息或两者为null依然返回false)</returns>
        public static bool InheritError<T>(this IKouError<T>? inheritor, IKouError<T>? decedent,
            string? supplement = null) where T : Enum
        {
            if (inheritor == null || decedent == null || !HasError((IKouErrorMsg) decedent))
            {
                if (supplement != null && inheritor != null)
                {
                    inheritor.ErrorMsg = supplement;
                    return false;
                }
                return false;
            }
            inheritor.ErrorCode = decedent.ErrorCode;
            inheritor.ErrorMsg = decedent.ErrorMsg + supplement?.Be("，" + supplement);
            return false;
        }

        /// <summary>
        /// 继承某个实现了IKouErrorMsg接口的类的错误
        /// </summary>
        /// <param name="inheritor">继承者</param>
        /// <param name="decedent">被继承者</param>
        /// <param name="supplement">补充，比如错误来源</param>
        /// <returns>必定返回false用于快速return (注意没有发现错误信息或两者为null依然返回false)</returns>
        public static bool InheritError<T>(this IKouError<T>? inheritor, IKouErrorMsg? decedent,
            string? supplement = null) where T : Enum
        {
            if (inheritor == null || decedent == null || !decedent.HasError())
            {
                if (supplement != null && inheritor != null)
                {
                    inheritor.ErrorMsg = supplement;
                    return false;
                }
                return false;
            }
            inheritor.ErrorMsg = decedent.ErrorMsg + supplement?.Be("，" + supplement);
            return false;
        }

        /// <summary>
        /// 检查是否有错误代码或错误信息提供（不能用ErrorCode == 0判断是否有错误）
        /// </summary>
        /// <param name="errorObject"></param>
        /// <returns>错误object为空也返回true</returns>
        [ContractAnnotation("errorObject:null => true")]
        public static bool HasError<T>(this IKouError<T>? errorObject) where T : Enum
        {
            if (errorObject == null) return true;
            return errorObject.ErrorCode.ToInt() != 0 || !string.IsNullOrWhiteSpace(errorObject.ErrorMsg);
        }
        /// <summary>
        /// 快速打包错误
        /// </summary>
        /// <param name="obj">使用this</param>
        /// <param name="errorMsg">错误内容</param>
        /// <param name="errorCode">错误码</param>
        /// <returns>必定返回false用于快速return</returns>
        //[ContractAnnotation("=> false")]
        public static bool ReturnFalseWithError<T>(this IKouError<T>? obj, string? errorMsg = null, T errorCode = default) where T : struct, Enum
        {
            if (obj == null) return false;
            obj.ErrorMsg = errorMsg;
            obj.ErrorCode = errorCode;
            return false;
        }
        /// <summary>
        /// 快速打包错误
        /// </summary>
        /// <param name="obj">使用this</param>
        /// <param name="errorMsg">错误内容</param>
        /// <param name="errorCode">错误码</param>
        /// <returns>必定返回null用于快速return</returns>
        //[ContractAnnotation("=> null")]
        public static dynamic? ReturnNullWithError<T>(this IKouError<T>? obj, string? errorMsg = null, T errorCode = default) where T : struct,Enum
        {
            if (obj == null) return null;
            obj.ErrorMsg = errorMsg;
            obj.ErrorCode = errorCode;
            return null;
        }
        /// <summary>
        /// 快速打包错误
        /// </summary>
        /// <param name="obj">使用this</param>
        /// <param name="errorCode">错误码</param>
        /// <returns>必定返回null用于快速return</returns>
        //[ContractAnnotation("=> null")]
        public static dynamic? ReturnNullWithError<T>(this IKouError<T>? obj, T errorCode) where T : Enum
        {
            if (obj == null) return null;
            obj.ErrorCode = errorCode;
            return null;
        }
        #endregion
    }
}