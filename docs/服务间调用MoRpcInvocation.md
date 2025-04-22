
```json
 "Services": {
   "FlightService.API": {
     "AppId": "service-flight-api"
   },
   "FlightService.ActorHost": {
     "AppId": "service-flight-actorhost"
   },
   "MessageService.API": {
     "AppId": "service-message-api"
   }
 }
```


#### HTTP 服务端实现

`[Get]`方法中请求DTO参数必须写`[FromQuery]`，不然会认为有`Body`而调用失败


# 自动代码生成

通过请求类生成RPC客户端代码。如果当前项目存在Handler，则仅生成RPC实现及其本地调用实现。

```cs
/// <summary>
/// 用户登录指令
/// </summary>
[Route("api/v1/user")]
[HttpPost("login")]
public record CommandLogin : IMoRequest<ResponseLogin>
{
    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
```

## 服务端侧

服务端侧HTTP实现

```cs
	[Route("api/v1/user")]
    [ApiController]
    public class HttpServerImplCommandUser(IMediator mediator) : ControllerBase
    {
     	/// <summary>
        /// 用户登录指令
        /// </summary>
        [HttpPost("login")]
        [ProducesResponseType((int) HttpStatusCode.Accepted)]
        [ProducesResponseType((int) HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(Res<ResponseLogin>), (int) HttpStatusCode.OK)]
        public async Task<ActionResult> Login(
            [FromBody] CommandLogin dto)
        {
            return await mediator.Send(dto).GetResponse(this);
    }
```

服务端侧RPC实现（该实现也可用于同领域应用服务调用)

```cs
 public class GrpcServerImplCommandUser(IMediator mediator) : ICommandUser
 {
     public async Task<Res<ResponseLogin>> Login(CommandLogin req)
     {
         return await _mediator.Send(query);
     }
 }
```

## 客户端侧

```cs
```

