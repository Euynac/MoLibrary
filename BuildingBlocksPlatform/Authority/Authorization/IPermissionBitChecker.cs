using System.Security.Claims;
using BuildingBlocksPlatform.Authority.Authorization.Annotations;

namespace BuildingBlocksPlatform.Authority.Authorization;

/// <summary>
/// 二进制权限检查器
/// </summary>
public interface IPermissionBitChecker<TEnum> where TEnum : struct, Enum
{
    /// <summary>
    /// 判断枚举型权限名是否存在于给定二进制位权限字符串中。
    /// </summary>
    /// <returns></returns>
    bool IsInBits(string permissionBits, string permissionNameOfEnum);
    /// <summary>
    /// 判断枚举型权限名是否存在于给定二进制位权限字符串中。
    /// </summary>
    /// <returns></returns>
    bool IsInBits(string permissionBits, TEnum permissionEnum);
    /// <summary>
    /// 判断枚举型权限名否都存在于给定二进制位权限字符串中。
    /// </summary>
    /// <returns></returns>
    bool IsInBits(string permissionBits, params TEnum[] permissionEnums);
    /// <summary>
    /// 判断枚举型权限名是否存在于给定ClaimsPrincipal的二进制位权限字符串中。
    /// </summary>
    bool IsGranted(ClaimsPrincipal principal, string permissionNameOfEnum);
    /// <summary>
    /// 判断枚举型权限是否存在于给定ClaimsPrincipal的二进制位权限字符串中。
    /// </summary>
    bool IsGranted(ClaimsPrincipal principal, TEnum permissionEnum);
    /// <summary>
    /// 判断给定枚举型权限是否都存在于给定ClaimsPrincipal的二进制位权限字符串中。
    /// </summary>
    bool IsGranted(ClaimsPrincipal principal, params TEnum[] permissionEnums);
    /// <summary>
    /// 从Claims中获取权限位字符串
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    string GetPermissionBits(ClaimsPrincipal principal);

    /// <summary>
    /// 获取超级管理员权限位字符串，即全为1
    /// </summary>
    /// <returns></returns>
    string GetAdminPermissionBits();

    /// <summary>
    /// 获取超级管理管理员权限Claims
    /// </summary>
    Claim GetAdminClaim();

    /// <summary>
    /// 转为权限位字符串
    /// </summary>
    /// <returns></returns>
    string ToPermissionBits(List<TEnum> permissionEnums);

    /// <summary>
    /// 获取已赋权的权限枚举列表
    /// </summary>
    /// <param name="permissionBits"></param>
    /// <returns></returns>
    List<TEnum> GrantedList(string permissionBits);
    /// <summary>
    /// 获取已赋权的权限枚举列表
    /// </summary>
    /// <returns></returns>
    List<TEnum> GrantedList(ClaimsPrincipal principal);
    /// <summary>
    /// 获取在给定范围内已赋权的权限枚举列表
    /// </summary>
    /// <returns></returns>
    List<TEnum> GrantedList(string permissionBits, params TEnum[] permissionScope);
    /// <summary>
    /// 获取在给定范围内已赋权的权限枚举列表
    /// </summary>
    /// <returns></returns>
    List<TEnum> GrantedList(ClaimsPrincipal principal, params TEnum[] permissionScope);
    /// <summary>
    /// 转为权限Claims
    /// </summary>

    /// <returns></returns>
    Claim ToClaim(List<TEnum> permissionEnums);

    /// <summary>
    /// 获取所有权限Bit定义信息
    /// </summary>
    /// <returns></returns>
    Dictionary<TEnum, IPermissionBitData> GetAllBitData();

    /// <summary>
    /// 获取权限Bit定义信息
    /// </summary>
    /// <param name="key">权限枚举名</param>
    /// <returns></returns>
    (TEnum, IPermissionBitData)? GetBitData(string key);

    /// <summary>
    /// 获取权限Bit定义信息
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    IPermissionBitData? GetBitData(TEnum key);
}
/// <summary>
/// 二进制权限检查器
/// </summary>
public interface IPermissionBitChecker 
{
    /// <summary>
    /// 判断枚举型权限名是否存在于给定二进制位权限字符串中。
    /// </summary>
    /// <returns></returns>
    bool IsInBits<TEnum>(string permissionBits, string permissionNameOfEnum) where TEnum : struct, Enum;
    /// <summary>
    /// 判断枚举型权限名是否存在于给定二进制位权限字符串中。
    /// </summary>
    /// <returns></returns>
    bool IsInBits<TEnum>(string permissionBits, TEnum permissionEnum) where TEnum : struct, Enum;
    /// <summary>
    /// 判断枚举型权限名否都存在于给定二进制位权限字符串中。
    /// </summary>
    /// <returns></returns>
    bool IsInBits<TEnum>(string permissionBits, params TEnum[] permissionEnums) where TEnum : struct, Enum;
    /// <summary>
    /// 判断枚举型权限名是否存在于给定ClaimsPrincipal的二进制位权限字符串中。
    /// </summary>
    bool IsGranted<TEnum>(ClaimsPrincipal principal, string permissionNameOfEnum) where TEnum : struct, Enum;
    /// <summary>
    /// 判断枚举型权限是否存在于给定ClaimsPrincipal的二进制位权限字符串中。
    /// </summary>
    bool IsGranted<TEnum>(ClaimsPrincipal principal, TEnum permissionEnum) where TEnum : struct, Enum;
    /// <summary>
    /// 判断给定枚举型权限是否都存在于给定ClaimsPrincipal的二进制位权限字符串中。
    /// </summary>
    bool IsGranted<TEnum>(ClaimsPrincipal principal, params TEnum[] permissionEnums) where TEnum : struct, Enum;
    /// <summary>
    /// 从Claims中获取权限位字符串
    /// </summary>
    /// <param name="principal"></param>
    /// <returns></returns>
    string GetPermissionBits<TEnum>(ClaimsPrincipal principal) where TEnum : struct, Enum;

    /// <summary>
    /// 获取超级管理员权限位字符串，即全为1
    /// </summary>
    /// <returns></returns>
    string GetAdminPermissionBits<TEnum>() where TEnum : struct, Enum;

    /// <summary>
    /// 获取超级管理管理员权限Claims
    /// </summary>
    Claim GetAdminClaim<TEnum>() where TEnum : struct, Enum;

    /// <summary>
    /// 转为权限位字符串
    /// </summary>
    /// <returns></returns>
    string ToPermissionBits<TEnum>(List<TEnum> permissionEnums) where TEnum : struct, Enum;

    /// <summary>
    /// 获取已赋权的权限枚举列表
    /// </summary>
    /// <param name="permissionBits"></param>
    /// <returns></returns>
    List<TEnum> GrantedList<TEnum>(string permissionBits) where TEnum : struct, Enum;
    /// <summary>
    /// 获取已赋权的权限枚举列表
    /// </summary>
    /// <returns></returns>
    List<TEnum> GrantedList<TEnum>(ClaimsPrincipal principal) where TEnum : struct, Enum;
    /// <summary>
    /// 获取在给定范围内已赋权的权限枚举列表
    /// </summary>
    /// <returns></returns>
    List<TEnum> GrantedList<TEnum>(string permissionBits, params TEnum[] permissionScope) where TEnum : struct, Enum;
    /// <summary>
    /// 获取在给定范围内已赋权的权限枚举列表
    /// </summary>
    /// <returns></returns>
    List<TEnum> GrantedList<TEnum>(ClaimsPrincipal principal, params TEnum[] permissionScope) where TEnum : struct, Enum;
    /// <summary>
    /// 转为权限Claims
    /// </summary>

    /// <returns></returns>
    Claim ToClaim<TEnum>(List<TEnum> permissionEnums) where TEnum : struct, Enum;

    /// <summary>
    /// 获取所有权限Bit定义信息
    /// </summary>
    /// <returns></returns>
    Dictionary<TEnum, IPermissionBitData> GetAllBitData<TEnum>() where TEnum : struct, Enum;

    /// <summary>
    /// 获取权限Bit定义信息
    /// </summary>
    /// <param name="key">权限枚举名</param>
    /// <returns></returns>
    (TEnum, IPermissionBitData)? GetBitData<TEnum>(string key) where TEnum : struct, Enum;

    /// <summary>
    /// 获取权限Bit定义信息
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    IPermissionBitData? GetBitData<TEnum>(TEnum key) where TEnum : struct, Enum;
}