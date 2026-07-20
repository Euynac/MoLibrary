# Monica.DataChannel 早期设计草案（已归档）

此文件原先记录 DataChannel 的早期设计设想，其中的 `IPipeEndpoint`、`DataContext`、`ICommunicationCore` 和 `CommunicationMetadata` 等名称已不再属于当前公开 API，请勿据此实现新代码。

请改为阅读以下当前资料：

- [README.md](README.md)：安装、宿主组合、发送数据和公开 API 快速入门。
- [DataChannel.Framework.md](DataChannel.Framework.md)：当前组件关系、`IDataChannelSetup` / `IDataChannelRegistrar` 注册模型、`ChannelDataContext`、`CommunicationOptions`、Provider 边界与宿主生命周期。

DataChannel 当前成熟度为 **Labs**，后续设计以源码和以上两份文档为准。
