# Unity 网络通信模块 (Network Module)

## 概述

本模块为 Unity 项目提供了一套包含 TCP 和 UDP 的基础网络通信解决方案，旨在帮助开发者快速搭建客户端与服务器的通信原型，或实现局域网内的设备间通信

它封装了底层的 Socket 操作，提供了独立的 `TCPClient`、`TCPServer` 和 `UDPController` 组件，支持事件驱动的消息处理和编辑器内模拟测试

## 核心功能

*   **双协议支持**: 同时提供 TCP 和 UDP 两种通信协议的实现，以适应不同场景的需求
*   **TCP C/S 架构**:
    *   提供 `TCPServer` 和 `TCPClient` 组件，支持一对多的可靠连接
    *   服务器可管理多个客户端，并支持向所有客户端广播或向特定客户端发送消息
    *   客户端与服务器均采用多线程处理网络消息，避免阻塞 Unity 主线程
*   **UDP 通信**:
    *   提供 `UDPController` 组件，用于无连接的、基于数据报的消息收发
    *   支持向局域网内任意 IP 端点发送消息
*   **事件驱动**: 通过 C# Action 和 UnityEvent 提供消息接收事件，方便将网络逻辑与业务逻辑解耦
*   **全局单例访问**: `TCPClient` 和 `TCPServer` 采用单例模式，方便在任何脚本中快速获取实例
*   **编辑器内调试**: 所有组件均提供模拟发送和接收数据的功能，方便在没有网络环境或对端的情况下进行开发和调试

## 组件说明

| 组件 | 协议 | 适用场景 |
| :--- | :--- | :--- |
| **`UDPController`** | UDP | 局域网内设备间的高频、低延迟通信，不要求数据绝对可靠，如状态同步、设备发现 |
| **`TCPServer`** | TCP | 作为游戏服务器或控制中心，需要管理多个客户端连接，并确保指令和数据可靠传输 |
| **`TCPClient`** | TCP | 作为客户端连接到 TCP 服务器，进行可靠的数据交换 |

---

## 快速开始

### 1. UDP 使用方法

1.  在场景中创建一个空对象，并添加 `UDPController` 组件，或直接从包的Prefabs目录下的预制件 `UDPController` 实例化（推荐做法）
2.  在 Inspector 中配置 `Local IP` 和 `Local Port` 作为接收端口，或保持默认
3.  勾选 `Open On Awake` 使其在启动时自动开始监听

```csharp
// 发送消息
using DreemurrStudio.Network;
using UnityEngine;

public class UDPSender : MonoBehaviour
{
    public UDPController udpController;

    void Start()
    {
        // 向指定IP和端口发送消息
        udpController.SendUDPMessage("192.168.1.101", 8080, "Hello from UDP");
    }
}
```

```csharp
// 接收消息
using DreemurrStudio.Network;
using UnityEngine;
using System.Net;

public class UDPReceiver : MonoBehaviour
{
    public UDPController udpController;

    void Start()
    {
        // 方式一：通过 C# Action 订阅 (推荐)
        udpController.onReceiveUDPMessage += HandleMessage;
    }

    private void HandleMessage(IPEndPoint sender, string message)
    {
        Debug.Log($"收到来自 {sender} 的消息: {message}");
    }

    void OnDestroy()
    {
        // 务必在销毁时取消订阅
        if (udpController != null)
        {
            udpController.onReceiveUDPMessage -= HandleMessage;
        }
    }
}
```

### 2. TCP 使用方法

#### 服务器端

1.  将Prefabs目录下的预制件 `TCPServer` 实例化（推荐做法），或在场景中创建空对象并添加 `TCPServer` 组件
2.  配置 `Port` 和 `Server IP`，勾选 `Start On Start` 使其自动运行

```csharp
// 服务器逻辑
using DreemurrStudio.Network;
using UnityEngine;

public class MyGameServer : MonoBehaviour
{
    void Start()
    {
        // 监听来自任意客户端的消息
        TCPServer.Instance.OnReceivedMessage += (message) =>
        {
            Debug.Log("服务器收到消息: " + message);

            // 将收到的消息广播给所有客户端
            TCPServer.Instance.SendToAllClients("服务器已收到: " + message);
        };
    }
}
```

#### 客户端

1.  将Prefabs目录下的预制件 `TCPClient` 实例化（推荐做法），或在场景中创建空对象并添加 `TCPClient` 组件
2.  配置 `Server IP` 和 `Server Port` 为服务器的地址，勾选 `Connect On Start`

```csharp
// 客户端逻辑
using DreemurrStudio.Network;
using UnityEngine;

public class MyGameClient : MonoBehaviour
{
    void Start()
    {
        // 监听来自服务器的消息
        TCPClient.Instance.OnReceivedMessage += (message) =>
        {
            Debug.Log("客户端收到服务器消息: " + message);
        };
    }

    // 通过UI按钮等调用
    public void SendLoginRequest()
    {
        if (TCPClient.Instance.IsConnected)
        {
            TCPClient.Instance.SendMessageToServer("请求登录");
        }
    }
}
```

### 3. 结构化消息

为了发送复杂数据，你可以创建继承自 `MessageDataBase` 的类，并使用 `JsonUtility` 进行序列化和反序列化
>对于更复杂的消息结构，可以考虑使用更强大的序列化库，推荐组合使用 Newtonsoft.Json + DreemurrStudio.Utilities 包

```csharp
// 1. 定义消息结构 (在 MessageDataCollection.cs 或其他地方)
[System.Serializable]
public class PlayerPosMessage : MessageDataBase
{
    public float x, y, z;
    public PlayerPosMessage()
    {
        messageType = MessageType.PlaceBuilding; // 举例
    }
}

// 2. 发送方：序列化并发送
var posMessage = new PlayerPosMessage { x = 1.0f, y = 2.5f, z = 0f };
string json = JsonUtility.ToJson(posMessage);
TCPClient.Instance.SendMessageToServer(json);

// 3. 接收方：反序列化并处理
TCPServer.Instance.OnReceivedMessage += (json) =>
{
    // 首先判断消息类型
    var baseMsg = JsonUtility.FromJson<MessageDataBase>(json);
    if (baseMsg.messageType == MessageType.PlaceBuilding)
    {
        var posMessage = JsonUtility.FromJson<PlayerPosMessage>(json);
        Debug.Log($"收到玩家位置: ({posMessage.x}, {posMessage.y})");
    }
};
```