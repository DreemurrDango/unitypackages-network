# Unity 网络通信模块 (Network Module)

## 概述

本模块为 Unity 项目提供了一套包含 TCP 和 UDP 的基础网络通信解决方案，旨在帮助开发者快速搭建客户端与服务器的通信原型，或实现局域网内的设备间通信

它封装了底层的 Socket 操作，提供了独立的 `TCPClient`、`TCPServer` 和 `UDPController` 组件，支持事件驱动的消息处理和编辑器内模拟测试

## 核心功能

*   **双协议支持**: 同时提供 TCP 和 UDP 两种通信协议的实现，以适应不同场景的需求
*   **TCP 粘包处理**: TCP 通信内置了基于“长度前缀”的协议，能自动处理粘包和断包问题，确保消息的完整性
*   **主线程调度器**: 提供 `UnityMainThreadDispatcher`，可以安全地将网络线程中接收到的任务（如更新UI）调度到 Unity 主线程执行
*   **二进制数据传输**: 支持直接发送和接收 `byte[]` 数组，方便传输序列化对象、文件或其他非文本数据
*   **生命周期事件**:
    *   `TCPClient` 提供 `OnConnectedToServer` 和 `OnDisconnectedFromServer` 事件
    *   `TCPServer` 提供 `OnClientConnected` 和 `OnClientDisconnected` 事件
    *   所有组件都提供 `OnReceivedData` 或 `OnReceivedMessage` 事件
*   **TCP C/S 架构**:
    *   提供 `TCPServer` 和 `TCPClient` 组件，支持一对多的可靠连接
    *   服务器可管理多个客户端，并支持向所有客户端广播或向特定客户端发送消息
    *   客户端与服务器均采用多线程处理网络消息，避免阻塞 Unity 主线程
*   **UDP 通信**:
    *   提供 `UDPController` 组件，用于无连接的、基于数据报的消息收发
*   **全局单例访问**: `TCPClient` 和 `TCPServer` 采用单例模式，方便在任何脚本中快速获取实例

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
using System.Text;

public class UDPSender : MonoBehaviour
{
    public UDPController udpController;

    void Start()
    {
        // 发送字符串消息
        udpController.SendUDPMessage("192.168.1.101", 8080, "Hello from UDP");

        // 发送二进制数据
        byte[] binaryData = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        udpController.SendUDPMessage("192.168.1.101", 8080, binaryData, "MyBinaryData");
    }
}
```

```csharp
// 接收消息
using DreemurrStudio.Network;
using UnityEngine;
using System.Net;
using System.Text;

public class UDPReceiver : MonoBehaviour
{
    public UDPController udpController;

    void Start()
    {
        // 方式一：通过 C# Action 订阅二进制数据 (推荐)
        udpController.onReceiveUDPData += HandleBinaryData;
        
        // 方式二：通过 UnityEvent 订阅字符串消息
        udpController.onReceiveMessage.AddListener(HandleStringMessage);
    }

    private void HandleBinaryData(IPEndPoint sender, byte[] data)
    {
        Debug.Log($"收到来自 {sender} 的二进制数据，长度: {data.Length}");
        // 如果确定是字符串，可以解码
        // string message = Encoding.UTF8.GetString(data);
    }

    private void HandleStringMessage(string message)
    {
        Debug.Log($"收到字符串消息: {message}");
    }

    void OnDestroy()
    {
        // 务必在销毁时取消订阅
        if (udpController != null)
        {
            udpController.onReceiveUDPData -= HandleBinaryData;
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
using System.Net;
using System.Text;

public class MyGameServer : MonoBehaviour
{
    void Start()
    {
        // 监听客户端连接事件
        TCPServer.Instance.OnClientConnected += (clientIP) =>
        {
            Debug.Log($"客户端 {clientIP} 已连接");
        };

        // 监听客户端断开事件
        TCPServer.Instance.OnClientDisconnected += (clientIP) =>
        {
            Debug.Log($"客户端 {clientIP} 已断开");
        };

        // 监听来自任意客户端的二进制数据
        TCPServer.Instance.OnReceivedData += (sender, data) =>
        {
            Debug.Log($"服务器收到来自 {sender} 的数据，长度: {data.Length}");
            string message = Encoding.UTF8.GetString(data);

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
using System.Text;
using System.Net;

public class MyGameClient : MonoBehaviour
{
    void Start()
    {
        // 监听连接成功事件
        TCPClient.Instance.OnConnectedToServer += (clientIP, serverIP) =>
        {
            Debug.Log($"成功连接到服务器 {serverIP}，本地端点为 {clientIP}");
        };

        // 监听与服务器断开事件
        TCPClient.Instance.OnDisconnectedFromServer += (clientIP, serverIP) =>
        {
            Debug.Log($"与服务器 {serverIP} 的连接已断开");
        };

        // 监听来自服务器的数据
        TCPClient.Instance.OnReceivedData += (data) =>
        {
            string message = Encoding.UTF8.GetString(data);
            Debug.Log("客户端收到服务器消息: " + message);
        };
    }

    // 通过UI按钮等调用
    public void SendLoginRequest()
    {
        if (TCPClient.Instance.IsConnected)
        {
            // 发送字符串
            TCPClient.Instance.SendToServer("请求登录");
            
            // 发送二进制数据
            byte[] binaryData = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
            TCPClient.Instance.SendToServer(binaryData, "LoginData");
        }
    }
}
```

### 3. 在网络回调中与 Unity 主线程交互

网络事件（如 `OnReceivedData`）是在后台线程中触发的。如果你想在收到消息后更新 UI 或操作场景中的物体，必须将这些任务交由 `UnityMainThreadDispatcher` 来执行。

```csharp
using DreemurrStudio.Network;
using UnityEngine;
using UnityEngine.UI;

public class TextureReceiver : MonoBehaviour
{
    public RawImage receivedImageDisplay;

    void Start()
    {
        TCPClient.Instance.OnReceivedData += OnImageDataReceived;
    }

    private void OnImageDataReceived(byte[] imageData)
    {
        // OnReceivedData 在后台线程被调用
        // 不能在这里直接操作 Unity UI
        
        // 使用调度器将 UI 更新任务排队到主线程
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            // 这部分代码将在下一帧的主线程中执行
            var tex = new Texture2D(2, 2);
            if (tex.LoadImage(imageData))
            {
                receivedImageDisplay.texture = tex;
                Debug.Log("图片加载成功！");
            }
        });
    }

    void OnDestroy()
    {
        if(TCPClient.Instance != null)
            TCPClient.Instance.OnReceivedData -= OnImageDataReceived;
    }
}
```