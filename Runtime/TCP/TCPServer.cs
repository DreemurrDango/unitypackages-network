using DreemurrStudio.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEditor.PackageManager;
using UnityEngine;

namespace DreemurrStudio.Network
{
    /// <summary>
    /// 使用TCP协议的服务器端通信脚本
    /// 提示：当需要处理不同类型的数据时，例如消息命令和图片传输，应当使用不同的TCP服务器实例进行处理，以避免数据混淆
    /// </summary>
    public class TCPServer : MonoBehaviour
    {
        /// <summary>
        /// 消息包头长度（用于存储消息长度的字节数）
        /// </summary>
        private const int MESSAGEHEADLENGTH = 4;
        /// <summary>
        /// 接收线程休眠时间（毫秒）
        /// </summary>
        private const int CLIENTLISTERNERTHREADSLEEPMS = 10;

        [SerializeField]
        [Tooltip("服务器的端口号")]
        private int port = 8888;
        [SerializeField]
        [Tooltip("服务器绑定的IP地址")]
        private string serverIP = "127.0.0.1";
        [SerializeField]
        [Tooltip("是否在开始时自动启动TCP服务器")]
        private bool startOnStart = true;

        [Header("消息处理")]
        [SerializeField]
        [Tooltip("接收数据时是否添加长度包头，启用时将收到的数据前4字节作为长度包头进行分段解析，应当在传输较大文本数据或二进制数据(图片、音频、视频流)时启用\n注意：若启用，则客户端发送时也需要对应地启用")]
        private bool useLengthHead = false;
        [SerializeField]
        [Tooltip("仅在未使用长度包头时生效，单条消息的最大长度（字节数B），超过该长度的消息将被丢弃")]
        private int maxMessageSize = 1024;

        [Header("调试")]
        [SerializeField]
        [Tooltip("是否显示完整调试信息")]
        private bool showFullDebug = false;

        [Tooltip("客户端连接时事件，参数为<客户端IP端点>")]
        public event Action<IPEndPoint> OnClientConnected;
        [Tooltip("客户端断开连接时事件，参数为<客户端IP端点>")]
        public event Action<IPEndPoint> OnClientDisconnected;
        [Tooltip("收到原始数据时事件，参数为<发送端IP端点,原始数据内容>")]
        public event Action<IPEndPoint, byte[]> OnReceivedRawData;
        [Tooltip("收到消息时事件，参数为消息内容")]
        public event Action<IPEndPoint,string> OnReceivedMessage;

        /// <summary>
        /// 是否正在运行TCP服务器
        /// </summary>
        private bool inRunning = false;
        /// <summary>
        /// TCP监听器
        /// </summary>
        private TcpListener listener;
        /// <summary>
        /// 监听线程
        /// </summary>
        private Thread listenThread;
        /// <summary>
        /// 所有已连接的客户端列表
        /// </summary>
        private Dictionary<IPEndPoint,(TcpClient client, NetworkStream stream)> connectIPClients = new();

        /// <summary>
        /// 当前是否正在运行服务器
        /// </summary>
        public bool InRunning => inRunning;
        /// <summary>
        /// 获取已连接的客户端数量
        /// </summary>
        public int ConnectedNum => connectIPClients.Count;
        /// <summary>
        /// 获取所有已连接的客户端列表
        /// </summary>
        public List<IPEndPoint> ConnectedIPEPs => InRunning ? null : new List<IPEndPoint>(connectIPClients.Keys);
        /// <summary>
        /// 获取服务器的IP端点
        /// </summary>
        public IPEndPoint ServerIPEP => new IPEndPoint(IPAddress.Parse(serverIP), port);

        private void Start()
        {
            UnityMainThreadDispatcher.Instance();
            if (startOnStart) StartServer(serverIP, port);
        }

        private void OnDestroy()
        {
            if (inRunning) StopServer();
        }

        /// <summary>
        /// 打开服务器
        /// </summary>
        public void StartServer(string ip, int port, bool useLengthHead = false) =>
            StartServer(new IPEndPoint(IPAddress.Parse(ip), port), useLengthHead);

        /// <summary>
        /// 打开服务器
        /// </summary>
        public void StartServer(IPEndPoint ipED,bool useLengthHead = false)
        {
            if (inRunning) return;
            inRunning = true;
            try
            {
                listener = new TcpListener(ipED);
                listener.Start();
                // 获取实际绑定的IP和端口
                var ipep = (IPEndPoint)listener.LocalEndpoint;
                this.serverIP = ipep.Address.ToString();
                this.port = ipep.Port;
                this.useLengthHead = useLengthHead;
                // 启动监听线程
                listenThread = new Thread(ListenForClients);
                listenThread.IsBackground = true;
                listenThread.Start();
                Debug.Log($"TCP服务器已启动：{ipep}");
            }
            catch (Exception e)
            {
                inRunning = false;
                Debug.LogError($"TCP服务器{ipED}启动失败:{e}");
            }
        }

        /// <summary>
        /// 停止运行服务器
        /// </summary>
        public void StopServer()
        {
            inRunning = false;
            listener?.Stop();
            foreach (var c in connectIPClients.Values)
                if (c.client != null && c.client.Connected) c.client.Close();
            connectIPClients.Clear();
            listenThread?.Join();
            Debug.Log($"TCP服务器{serverIP}:{port}已关闭");
        }

        /// <summary>
        /// 监听客户端连接线程程序
        /// </summary>
        private void ListenForClients()
        {
            while (inRunning)
            {
                try
                {
                    if (listener.Pending())
                    {
                        // 接受新的客户端连接
                        var client = listener.AcceptTcpClient();
                        var stream = client.GetStream();
                        lock (connectIPClients)
                        {
                            connectIPClients.Add((IPEndPoint)client.Client.RemoteEndPoint, (client, stream));
                        }
                        // 获取客户端IP端点
                        var clientIPEP = (IPEndPoint)client.Client.RemoteEndPoint;
                        Debug.Log("客户端已连接: " + clientIPEP);
                        // 在主线程执行用户回调
                        UnityMainThreadDispatcher.Instance().Enqueue(() =>
                        {
                             OnClientConnected?.Invoke(clientIPEP);
                        });
                        // 启动处理该客户端通信的线程
                        var clientThread = new Thread(() => HandleClientComm(client, stream, useLengthHead));
                        clientThread.IsBackground = true;
                        clientThread.Start();
                    }
                    Thread.Sleep(CLIENTLISTERNERTHREADSLEEPMS);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("客户端启动监听异常: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 处理收到的客户端消息
        /// </summary>
        /// <param name="client">监听的客户端</param>
        /// <param name="stream">与该客户端的通信数据流</param>
        private void HandleClientComm(TcpClient client, NetworkStream stream, bool useLengthHead)
        {
            var clientIPEP = (IPEndPoint)client.Client.RemoteEndPoint;
            byte[] buffer = useLengthHead ? new byte[MESSAGEHEADLENGTH] : new byte[maxMessageSize];            
            try
            {
                while (inRunning && client.Connected)
                {
                    try 
                    {
                        byte[] messageData = null;
                        if (useLengthHead)
                        {
                            // 1. 读取包头，获取包体长度
                            int bytesRead = ReadFull(stream, buffer, MESSAGEHEADLENGTH);
                            if (bytesRead < MESSAGEHEADLENGTH) break;
                            int messageLength = BitConverter.ToInt32(buffer, 0);
                            if (messageLength <= 0) continue;
                            if (showFullDebug) Debug.Log($"{serverIP}:{port}接收到来自{clientIPEP}的带包头数据，包体长度：{messageLength}字节");

                            // 2. 根据包体长度读取包体
                            messageData = new byte[messageLength];
                            bytesRead = ReadFull(stream, messageData, messageLength);
                            if (bytesRead < messageLength) break; // 数据不中断
                        }
                        else
                        {
                            // 1. 直接读取数据流
                            int bytesRead = stream.Read(buffer, 0, buffer.Length);
                            if (bytesRead == 0) break; // 连接断开
                            if (showFullDebug) Debug.Log($"{serverIP}:{port}接收到{bytesRead}字节长度的数据");
                            // 仅拷贝实际读取的数据
                            messageData = new byte[bytesRead];
                            Buffer.BlockCopy(buffer, 0, messageData, 0, bytesRead);
                        }
                        // 3. 触发事件 (分发到主线程)
                        if (messageData != null)
                        {
                            UnityMainThreadDispatcher.Instance().Enqueue(() => HandleReceivedData(clientIPEP,messageData));
                        }
                    }
                    catch (IOException ioe)
                    {
                        //Debug.LogError($"与客户端{clientIPEP}的连接已中断:{ioe}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("客户端通信异常: " + ex.Message);
            }
            finally
            {
                Debug.Log($"客户端通信结束: {clientIPEP}");
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    OnClientDisconnected?.Invoke(clientIPEP);
                });                
                lock (connectIPClients)
                {
                    connectIPClients.Remove(clientIPEP);
                }
                client.Close();
                Debug.Log($"客户端已断开: {clientIPEP}");
            }
        }

        /// <summary>
        /// 辅助方法：确保读取指定长度的数据
        /// </summary>
        private int ReadFull(NetworkStream stream, byte[] buffer, int length)
        {
            int totalRead = 0;
            while (totalRead < length)
            {
                int read = stream.Read(buffer, totalRead, length - totalRead);
                if (read == 0) return totalRead;
                totalRead += read;
            }
            return totalRead;
        }

        /// <summary>
        /// 对收到的数据进行处理
        /// </summary>
        /// <param name="clientIPEP">客户端IP端点</param>
        /// <param name="data">收到的数据</param>
        private void HandleReceivedData(IPEndPoint clientIPEP, byte[] data)
        {
            // 触发收到数据事件
            OnReceivedRawData?.Invoke(clientIPEP, data);
            if (OnReceivedMessage != null || showFullDebug)
            {
                string message = Encoding.UTF8.GetString(data);
                Debug.Log($"收到客户端{clientIPEP}的消息: {message}");
                OnReceivedMessage?.Invoke(clientIPEP, message);
            }
        }

        /// <summary>
        /// 向指定客户端发送消息
        /// </summary>
        /// <param name="aimIPEP">要发送到的目标IP端点</param>
        /// <param name="message">要发送的客户端信息</param>
        /// <param name="useLengthHead">是否使用长度包头，可指定为空，以使用服务器的设置</param>
        /// <param name="debugRemake">调试标记，用于标记当前发送的内容信息，只做日志输出，不会传输</param>
        public void SendToClient(IPEndPoint aimIPEP, string message,bool? useLengthHead = false,string debugRemake = "文本消息") => SendToClient(aimIPEP, Encoding.UTF8.GetBytes(message), useLengthHead, debugRemake);

        /// <summary>
        /// 向特定客户端发送原始字节数据
        /// </summary>
        /// <param name="aimIPEP">要发送的客户端目标</param>
        /// <param name="data">要发送的二进制字节数组数据</param>
        /// <param name="useLengthHead">是否使用长度包头</param>"
        /// <param name="debugRemake">可附加的调试信息</param>
        public void SendToClient(IPEndPoint aimIPEP, byte[] data,bool? useLengthHead = false,string debugRemake = "")
        {
            try
            {
                TcpClient client = null;
                NetworkStream stream = null;
                lock (connectIPClients)
                {
                    if (!connectIPClients.TryGetValue(aimIPEP, out var clientInfo))
                    {
                        Debug.LogWarning($"客户端{aimIPEP}未连接至服务器，无法发送消息");
                        return;
                    }
                    client = clientInfo.client;
                    stream = clientInfo.stream;
                }
                if (client == null || !client.Connected) return;
                // 准备发送数据
                byte[] fullData = data;
                int prefixLength = 0;
                if(useLengthHead ?? this.useLengthHead)
                {
                    // 1. 准备长度包头
                    var lengthPrefix = BitConverter.GetBytes(data.Length);
                    fullData = new byte[lengthPrefix.Length + data.Length];
                    // 2. 发送包头和数据
                    Buffer.BlockCopy(lengthPrefix, 0, data, 0, lengthPrefix.Length);
                    prefixLength = lengthPrefix.Length;
                }
                Buffer.BlockCopy(data, 0, fullData, prefixLength, data.Length);
                stream.Write(fullData, 0, fullData.Length);
                // 输出调试信息
                debugRemake = string.IsNullOrEmpty(debugRemake) ? "" : $"[{debugRemake}]";
                Debug.Log($"已向[{client.Client.RemoteEndPoint}]发送TCP消息 {debugRemake}({(prefixLength > 0 ? prefixLength + "+" : "")}{data.Length}B)");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"发送消息{debugRemake}失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 向所有已连接客户端发送消息
        /// </summary>
        /// <param name="message">要发送的消息内容</param>
        /// <param name="debugRemake">调试标记，用于标记当前发送的内容信息，只做日志输出，不会传输</param>
        /// <param name="useLengthHead">是否使用长度包头，可指定为空，以使用服务器的设置</param>
        public void SendToAllClients(string message,bool? useLengthHead = null,string debugRemake = "文本消息") => SendToAllClients(Encoding.UTF8.GetBytes(message), useLengthHead, debugRemake);

        /// <summary>
        /// 向所有客户端广播原始字节数据
        /// </summary>
        /// <param name="data">要发送的原始字节数据</param>
        /// <param name="useLengthHead">是否使用长度包头，可指定为空，以使用服务器的设置</param>
        /// <param name="debugRemake">调试标记，用于标记当前发送的内容信息，只做日志输出，不会传输</param>
        public void SendToAllClients(byte[] data,bool? useLengthHead = null, string debugRemake = "")
        {
            int prefixLength = 0;
            byte[] fullData = data;
            // 1. 准备好发送数据
            if (useLengthHead ?? this.useLengthHead)
            {
                // 使用长度包头
                byte[] lengthPrefix = BitConverter.GetBytes(data.Length);
                fullData = new byte[lengthPrefix.Length + data.Length];
                Buffer.BlockCopy(lengthPrefix, 0, fullData, 0, lengthPrefix.Length);
                prefixLength = lengthPrefix.Length;
            }
            Buffer.BlockCopy(data, 0, fullData, prefixLength, data.Length);
            var remake = string.IsNullOrEmpty(debugRemake) ? "" : $"[{debugRemake}]";
            // 2. 发送到所有客户端
            int successCount = 0;
            lock (connectIPClients)
            {
                foreach (var pair in connectIPClients)
                {
                    TcpClient client = pair.Value.client;
                    NetworkStream stream = pair.Value.stream;
                    if (client.Connected)
                    {
                        try
                        {
                            stream.Write(fullData, 0, fullData.Length);
                            if (showFullDebug) Debug.Log($"已向[{client.Client.RemoteEndPoint}]发送TCP消息{remake}");
                            successCount++;
                        }
                        catch (Exception ex)
                        {
                            if (showFullDebug) Debug.LogWarning($"向{client.Client.RemoteEndPoint}广播时消息{remake}失败:{ex}");
                        }
                    }
                }
            }
            Debug.Log($"已向{successCount}个客户端发送TCP消息 {remake}({(prefixLength > 0 ? prefixLength + "+" : "")}{data.Length}B)");
        }

        #region 模拟测试
#if UNITY_EDITOR
        [Header("模拟接收")]
        [Tooltip("用于测试的发送目标IP地址")]
        public string receiveIP;
        [Tooltip("用于测试的发送目标端地址")]
        public int receivePort;
        [Tooltip("用于测试的发送目标IP地址")]
        public string receivedMessage;

        [ContextMenu("模拟接收")]
        public void SimulateReceive() => HandleReceivedData(new IPEndPoint(IPAddress.Parse(receiveIP), receivePort), Encoding.UTF8.GetBytes(receivedMessage));

        [Header("模拟发送")]
        [Tooltip("用于测试的发送消息内容")]
        public string sendMessage = "模拟发送消息";

        [ContextMenu("模拟发送")]
        public void SimulateSend() => SendToAllClients(sendMessage);
#endif 
        #endregion
    }
}