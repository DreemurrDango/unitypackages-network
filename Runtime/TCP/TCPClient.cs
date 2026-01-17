using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using DreemurrStudio.Utilities;

namespace DreemurrStudio.Network
{
    /// <summary>
    /// 使用TCP协议的客户端通信脚本
    /// </summary>
    public class TCPClient : Singleton<TCPClient>
    {
        /// <summary>
        /// 消息包头长度（用于存储消息长度的字节数）
        /// </summary>
        private const int MESSAGEHEADLENGTH = 4;

        [Header("客户端设置")]
        [SerializeField]
        [Tooltip("是否自动获取客户端IP端点")]
        private bool autoGetClientIPEP;
        [SerializeField]
        [Tooltip("客户端绑定的IP地址，若置空将自动寻找可用IP")]
        private string clientIP = "127.0.0.1";
        [SerializeField]
        [Tooltip("客户端绑定的端口号，若置0将自动分配可用端口")]
        private int clientPort = 8888;
        [SerializeField]
        [Tooltip("是否在Start时自动连接服务器")]
        private bool connectOnStart = true;

        [Header("消息处理")]
        [SerializeField]
        [Tooltip("接收数据时是否添加长度包头，启用时将收到的数据前4字节作为长度包头进行分段解析，应当在传输较大文本数据或二进制数据(图片、音频、视频流)时启用\n注意：若启用，则客户端发送时也需要对应地启用")]
        private bool useLengthHead = false;
        [SerializeField]
        [Tooltip("仅在未使用长度包头时生效，单条消息的最大长度（字节数B），超过该长度的消息将被丢弃")]
        private int maxMessageSize = 1024;

        [Tooltip("连接到服务器时的动作，参数为<客户端IP端点,服务器IP端点>")]
        public event Action<IPEndPoint,IPEndPoint> OnConnectedToServer;
        [Tooltip("收到来自服务器的原始字节数据时的动作")]
        public event Action<byte[]> OnReceivedRawData;
        [Tooltip("收到来自服务器的事件时的动作")]
        public event Action<string> OnReceivedMessage;
        [Tooltip("与服务器断开连接时的动作，参数依次为客户端IP端点和服务器IP端点")]
        public event Action<IPEndPoint,IPEndPoint> OnDisconnectedFromServer;

        [Header("调试")]
        [SerializeField]
        [Tooltip("连接到的服务器的IP地址")]
        private string serverIP = "127.0.0.1";
        [SerializeField]
        [Tooltip("连接到的服务器的端口号")]
        private int serverPort = 8888;
        [SerializeField]
        [Tooltip("是否显示完整的调试信息")]
        private bool showFullDebug = false;

        /// <summary>
        /// tcp客户端
        /// </summary>
        private TcpClient client;
        /// <summary>
        /// 发送和接收数据的网络流
        /// </summary>
        private NetworkStream stream;
        /// <summary>
        /// 接收数据的线程
        /// </summary>
        private Thread receiveThread;

        /// <summary>
        /// 当前是否已连接到服务器
        /// </summary>
        public bool IsConnected => client != null && client.Connected;
        /// <summary>
        /// 获取客户端的IP端点
        /// </summary>
        public IPEndPoint ClientIPEP => new IPEndPoint(IPAddress.Parse(clientIP), clientPort);
        /// <summary>
        /// 获取当前连接到的服务器IP端点
        /// </summary>
        public IPEndPoint ServerIPEP => client != null && client.Connected ? new IPEndPoint(IPAddress.Parse(serverIP), serverPort) : null;

        private void Start()
        { 
            // 确保调度器已初始化
            UnityMainThreadDispatcher.Instance();
            if (connectOnStart)
            {
                // 异步连接，防止卡死主线程
                Task.Run(() => ConnectToServer());
            }
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        /// <summary>
        /// 连接到服务器
        /// </summary>
        /// <param name="serverIPEP">要连接到的服务器IP端点</param>
        /// <param name="clientIPEP">本地客户端IP端点</param>
        public bool ConnectToServer(IPEndPoint serverIPEP,IPEndPoint clientIPEP)
        {
            if (IsConnected) return true;
            try
            {
                client = clientIPEP == null ? new TcpClient() : new TcpClient(clientIPEP);
                // TODO:同步连接会阻塞，建议在Task中调用
                client.Connect(serverIPEP);
                stream = client.GetStream();                
                // 获取实际连接的服务器IP端点
                var sep = (IPEndPoint)client.Client.RemoteEndPoint;
                this.serverIP = sep.Address.ToString();
                this.serverPort = sep.Port;
                // 获取实际绑定的客户端IP端点
                var cip = (IPEndPoint)client.Client.LocalEndPoint;
                this.clientIP = cip.Address.ToString();
                this.clientPort = cip.Port;                
                // 启动接收线程
                receiveThread = new Thread(() => ListenningMessage(useLengthHead));
                receiveThread.IsBackground = true;
                receiveThread.Start();
                Debug.Log("已连接到服务器: " + serverIP + ":" + serverPort);
                // 分发事件到主线程
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    OnConnectedToServer?.Invoke(cip, sep);
                });
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"尝试连接到服务器{serverIPEP}失败:{ex.Message} ");
                Disconnect(); // 连接失败时也确保清理
                return false;
            }
        }
        /// <summary>
        /// 使用预设的客户端IP和端口连接到服务器
        /// </summary>
        /// <param name="serverIPEP">要连接到的服务器IP端点</param>
        public bool ConnectToServer(IPEndPoint serverIPEP) => ConnectToServer(serverIPEP, autoGetClientIPEP ? null : new IPEndPoint(IPAddress.Parse(clientIP), clientPort));
        /// <summary>
        /// 使用预设的客户端IP和端口连接到指定IP和端口的服务器
        /// </summary>
        /// <param name="serverIP">服务器IP地址</param>
        /// <param name="serverPort">服务器端口号</param>
        public bool ConnectToServer(string serverIP,int serverPort) => 
            ConnectToServer(new IPEndPoint(IPAddress.Parse(serverIP), serverPort), autoGetClientIPEP ? null : new IPEndPoint(IPAddress.Parse(clientIP), clientPort));
        /// <summary>
        /// 使用指定的客户端IP和端口连接到指定IP和端口的服务器
        /// </summary>
        /// <param name="serverIP">服务器IP地址</param>
        /// <param name="serverPort">服务器端口号</param>
        /// <param name="clientIP">客户端IP地址</param>
        /// <param name="clientPort">客户端端口号</param>
        public bool ConnectToServer(string serverIP, int serverPort, string clientIP, int clientPort) =>
            ConnectToServer(new IPEndPoint(IPAddress.Parse(serverIP), serverPort), new IPEndPoint(IPAddress.Parse(clientIP), clientPort));
        [ContextMenu("连接服务器")]
        /// <summary>
        /// 使用预设的服务器和客户端IP端点连接到服务器
        /// </summary>
        /// <returns></returns>
        public bool ConnectToServer() => ConnectToServer(serverIP, serverPort, clientIP, clientPort);

        [ContextMenu("断开连接")]
        /// <summary>
        /// 与服务器断开连接
        /// </summary>
        public void Disconnect()
        {
            if (!IsConnected) return;
            try
            {
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    OnDisconnectedFromServer?.Invoke(ClientIPEP, ServerIPEP);
                });
                stream?.Close();
                client?.Close();
                // 不建议Join自己的接收线程，容易死锁，尤其是如果Disconnect被接收线程调用时
                if (receiveThread != null && Thread.CurrentThread != receiveThread)
                    receiveThread.Join(500);
            }
            catch (Exception e)
            {
                Debug.LogError("断开连接时发生错误: " + e.Message);
            }
            finally
            {
                stream = null;
                client = null;
                receiveThread = null;
                Debug.Log("已断开与服务器的连接");
            }
        }

        /// <summary>
        /// 接收服务器消息
        /// </summary>
        private void ListenningMessage(bool useLengthHead)
        {
            byte[] lengthBuffer = useLengthHead ? new byte[MESSAGEHEADLENGTH] : new byte[maxMessageSize];
            try
            {
                while (IsConnected)
                {
                    try
                    {
                        byte[] messageData = null;
                        if(useLengthHead)
                        {
                            // 1. 读取4字节的包头（数据长度）
                            int bytesRead = ReadFull(stream, lengthBuffer, MESSAGEHEADLENGTH);
                            if (bytesRead < MESSAGEHEADLENGTH) break;   
                            int messageLength = BitConverter.ToInt32(lengthBuffer, 0);
                            if (messageLength <= 0) continue;
                            if (showFullDebug) Debug.Log($"接收到来自服务器的带包头数据，包体长度: {messageLength}B");

                            // 2. 根据长度读取完整的数据
                            messageData = new byte[messageLength];
                            bytesRead = ReadFull(stream, messageData, messageLength);
                            if (bytesRead < messageLength) break; // 数据不完整
                        }
                        else
                        {
                            // 1. 直接读取可用数据
                            int bytesRead = stream.Read(lengthBuffer, 0, lengthBuffer.Length);
                            if (bytesRead == 0) break; // 连接断开
                            if(showFullDebug) Debug.Log($"接收到来自服务器的数据({bytesRead}B)");
                            messageData = new byte[bytesRead];
                            Buffer.BlockCopy(lengthBuffer, 0, messageData, 0, bytesRead);
                        }
                        // 3. 触发事件 (使用Dispatcher)
                        if(messageData != null)
                        {
                            UnityMainThreadDispatcher.Instance().Enqueue(() => HandleReceivedData(messageData));
                        }                        
                    }
                    catch (IOException ioe)
                    {
                        //Debug.LogError($"与服务器的连接意外关闭: {ioe}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("通信数据异常: " + ex.Message);
            }
            finally
            {
                Debug.Log("连接已关闭");
                // 确保在线程退出时执行断开逻辑
                if (IsConnected)
                    UnityMainThreadDispatcher.Instance(false)?.Enqueue(Disconnect);
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
        /// 处理收到的数据
        /// </summary>
        /// <param name="data">收到的原始字节数据</param>
        private void HandleReceivedData(byte[] data)
        {
            OnReceivedRawData?.Invoke(data);
            if (OnReceivedMessage != null || showFullDebug)
            {
                string message = Encoding.UTF8.GetString(data);
                Debug.Log("收到服务器消息: " + message);
                OnReceivedMessage?.Invoke(message);
            }
        }

        /// <summary>
        /// 发送原始字节数据到服务器
        /// </summary>
        /// <param name="data">发送的原始字节数据</param>
        /// <param name="debugRemark">日志输出信息，不需要时可不填</param>
        public void SendToServer(byte[] data, bool? useLengthHead = null, string debugRemark = "")
        {
            if (!IsConnected || stream == null) return;
            try
            {
                byte[] fullData = data;
                int prefixLength = 0;
                if (useLengthHead ?? this.useLengthHead)
                {
                    // 组合长度包头和数据
                    byte[] lengthPrefix = BitConverter.GetBytes(data.Length); // int32占4字节
                    prefixLength = lengthPrefix.Length;
                    fullData = new byte[prefixLength + data.Length];
                    Buffer.BlockCopy(lengthPrefix, 0, fullData, 0, prefixLength);
                }
                // 发送数据
                Buffer.BlockCopy(data, 0, fullData, prefixLength, data.Length);
                stream.Write(fullData, 0, fullData.Length);
                // 调试日志输出
                debugRemark = string.IsNullOrEmpty(debugRemark) ? "" : $"[{debugRemark}]";
                Debug.Log($"已向服务器发送数据{debugRemark}({data.Length})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("发送数据失败: " + ex.Message);
            }
        }
        /// <summary>
        /// 发送消息到服务器
        /// </summary>
        /// <param name="message">要直接发送的信息</param>
        /// <param name="useLengthHead">是否使用长度包头，若不指定则使用预设值</param>
        /// <param name="debugRemark">日志输出信息，不需要时可不填</param>
        public void SendToServer(string message, bool? useLengthHead = null, string debugRemark = "文本消息") => SendToServer(Encoding.UTF8.GetBytes(message), useLengthHead, debugRemark);


        #region 模拟测试
#if UNITY_EDITOR
        [Header("模拟接收")]
        [Tooltip("用于测试的发送目标IP地址")]
        public string receivedMessage;

        [ContextMenu("模拟接收")]
        public void SimulateReceive() => HandleReceivedData(Encoding.UTF8.GetBytes(receivedMessage));

        [Header("模拟发送")]
        [Tooltip("用于测试的发送消息内容")]
        public string sendMessage = "模拟发送消息";

        [ContextMenu("模拟发送")]
        public void SimulateSend() => SendToServer(sendMessage);
#endif 
        #endregion
    }
}