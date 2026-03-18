using Monica.Tool.Extensions;

namespace Monica.Authority.Identity.Models;

public class MoSystemUserOptions
{
    public Type? SystemUserEnums { get; private set; } 
    public object? CurrentSystemUserEnum { get; set; }
    public Dictionary<object, SystemUserInfo> InfoDict { get; set; } = [];

    public class SystemUserInfo
    {
        public required string Username { get; set; }
        public required string UserId { get; set; }
        public required string NickName { get; set; }
    }

    public void SetCurSystemUser<T>(T curSystemUser) where T : struct, Enum
    {
        SystemUserEnums = typeof(T);
        CurrentSystemUserEnum = curSystemUser;
        foreach (var (i, key) in Enum.GetValues<T>().WithIndex())
        {
            var index = i + 1;
            InfoDict.Add(key, new SystemUserInfo
            {
                Username = key.ToString(),
                UserId = Guid.Empty.ToString()[..^index.ToString().Length] + index,
                NickName = key.GetDescription()!
            });
        }

    }

    
}