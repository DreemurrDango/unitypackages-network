# 更新日志

> 此文件记录了该软件包所有重要的变更
> 文件格式基于 [Keep a Changelog](http://keepachangelog.com/en/1.0.0/) 更新日志规范，且此项目版本号遵循 [语义化版本](http://semver.org/spec/v2.0.0.html) 规范

## [2.2.0] - 2025-12-29
### 新增
- **TCP 粘包/断包处理**: 为 `TCPClient` 和 `TCPServer` 添加了基于长度前缀的协议。现在发送的每条消息都会自动附加一个4字节的包头来表示消息长度，以从根本上解决 TCP 传输中可能出现的粘包和断包问题，确保消息的完整性
- **二进制数据事件**: `TCPClient` 和 `TCPServer` 现在新增了 `OnReceivedData` 事件，专门用于处理原始的 `byte[]` 数据，这对于传输图片、文件等非文本数据至关重要
- **示例场景**: 添加了 `TextureTransmitTest` 示例，演示了如何利用新功能在客户端和服务器之间传输和显示图片

### 更改
- **`TCPClient` / `TCPServer`**: `Send` 和 `Receive` 的底层逻辑已重构，以支持新的“长度前缀”协议
- **`TCPClient`**: 增强了 `Disconnect` 和 `ListenningMessage` 中的异常处理和资源清理逻辑，使其在连接意外断开时更加健壮

## [2.1.0] - 2025-12-26
### 新增
- **二进制数据传输**: 为 `TCPClient`、`TCPServer` 和 `UDPController` 添加了直接发送和接收 `byte[]` (字节数组) 的功能。
    - `TCPClient` 添加了 `SendDataToServer(byte[] data, ...)` 方法和 `OnReceivedData` 事件
    - `TCPServer` 添加了 `SendToClient(..., byte[] data, ...)`、`SendToAllClients(byte[] data, ...)` 方法和 `OnReceivedData` 事件
    - `UDPController` 添加了 `SendUDPMessage(..., byte[] data, ...)` 方法和 `onReceiveUDPData` 事件

### 变更
- **消息数据结构**: `MessageDataCollection.cs` 示例脚本已从包的核心脚本中分离，并打包成一个独立的 `.unitypackage`。用户在导入网络包后，可以根据需要手动导入此示例包，以获得C/S架构的消息基类模板。这提高了包的模块化程度，避免了不必要的脚本导入

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