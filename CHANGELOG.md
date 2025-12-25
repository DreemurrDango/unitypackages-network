# 更新日志

> 此文件记录了该软件包所有重要的变更
> 文件格式基于 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/) 更新日志规范，且此项目版本号遵循 [语义化版本](http://semver.org/spec/v2.0.0.html) 规范

## [2.0.0] - 2025-12-24
### 新增
- **TCP 通信支持**: 添加了完整的 TCP 客户端 (`TCPClient`) 和服务器 (`TCPServer`) 功能
- **TCP 服务器**:
    - 支持启动和停止服务器，并管理多个客户端连接
    - 能够向所有客户端广播消息 (`SendToAllClients`) 或向特定客户端发送消息 (`SendToClient`)
    - 在单独的线程中处理客户端连接和消息接收，以避免阻塞主线程
- **TCP 客户端**:
    - 支持连接到服务器和断开连接
    - 在后台线程中自动监听来自服务器的消息
- **消息数据结构**: 添加了 `MessageDataCollection.cs`，为 C/S 架构提供了网络消息的基类 (`C2SMessageDataBase`, `S2CMessageDataBase`) 和消息类型枚举，便于进行结构化数据通信
- **单例模式**: `TCPClient` 和 `TCPServer` 继承自 `Singleton`，方便在项目中全局访问实例

## [1.0.0] - 2025-10-09
### 新增
- **UDP 通信支持**: 初始版本发布，实现了基于 UDP 的网络通信功能
- **UDP 控制器 (`UDPController`)**:
    - 支持打开和关闭 UDP 端口
    - 支持通过 `SendUDPMessage` 方法向指定 IP 和端口发送消息
    - 通过协程持续接收数据，并通过 `onReceiveMessage` (UnityEvent) 和 `onReceiveUDPMessage` (C# Action) 两种事件回调来处理接收到的消息
    - 支持在 Inspector 面板中配置本地 IP 和端口，并可选择在启动时自动打开端口
- **编辑器调试**: 为 `UDPController` 添加了在编辑器模式下模拟发送和接收消息的功能，方便开发调试