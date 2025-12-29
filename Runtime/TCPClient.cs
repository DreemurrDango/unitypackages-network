using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DreemurrStudio.Network
{
    // TODO:添加ID标记
    /// <summary>
    /// 使用TCP协议的客户端通信脚本
    /// </summary>
    public class TCPClient : Singleton<TCPClient>
    {
        /// <summary>
        /// 消息包头长度（用于存储消息长度的字节数）
        /// </summary>
        private const int MESSAGEHEADLENGTH = 4;

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
        [Tooltip("收到来自服务器的事件时的动作")]
        public Action<string> OnReceivedMessage;
        [Tooltip("收到来自服务器的原始字节数据时的动作")]
        public Action<byte[]> OnReceivedData;

        [Header("调试")]
        [SerializeField]
        [Tooltip("连接到的服务器的IP地址")]
        private string serverIP = "127.0.0.1";
        [SerializeField]
        [Tooltip("连接到的服务器的端口号")]
        private int serverPort = 8888;

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
        public IPEndPoint ServerIPEP => client.Connected ? new(IPAddress.Parse(serverIP), serverPort) : null;

        private void Start()
        {
            if (connectOnStart)
                ConnectToServer(new IPEndPoint(IPAddress.Parse(serverIP), serverPort), autoGetClientIPEP ? null : new IPEndPoint(IPAddress.Parse(clientIP), clientPort));
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
                client.Connect(serverIP, serverPort);
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
                receiveThread = new Thread(ListenningMessage);
                receiveThread.IsBackground = true;
                receiveThread.Start();
                Debug.Log("已连接到服务器: " + serverIP + ":" + serverPort);
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

        /// <summary>
        /// 与服务器断开连接
        /// </summary>
        public void Disconnect()
        {
            if (client == null) return;
            try
            {
                stream?.Close();
                client?.Close();
                receiveThread?.Join();
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
        private void ListenningMessage()
        {
            byte[] lengthBuffer = new byte[MESSAGEHEADLENGTH]; // 4字节用于存储消息长度
            try
            {
                while (IsConnected)
                {
                    if (stream.DataAvailable)
                    {
                        // 1. 读取4字节的包头（数据长度）
                        int bytesRead = stream.Read(lengthBuffer, 0, lengthBuffer.Length);
                        if (bytesRead < MESSAGEHEADLENGTH) break; // 连接断开或数据不完整
                        int messageLength = BitConverter.ToInt32(lengthBuffer, 0);

                        // 2. 根据长度读取完整的数据
                        var messageBuffer = new byte[messageLength];
                        int totalBytesRead = 0;
                        while (totalBytesRead < messageLength)
                        {
                            bytesRead = stream.Read(messageBuffer, totalBytesRead, messageLength - totalBytesRead);
                            if (bytesRead == 0) break; // 连接断开
                            totalBytesRead += bytesRead;
                        }
                        if (totalBytesRead < messageLength) break; // 数据不完整

                        // 3. 触发事件
                        OnReceivedData?.Invoke(messageBuffer);
                        if (OnReceivedMessage != null)
                        {
                            string message = Encoding.UTF8.GetString(messageBuffer);
                            Debug.Log("收到服务器消息: " + message);
                            OnReceivedMessage?.Invoke(message);
                        }
                    }
                    Thread.Sleep(10);
                }
            }
            catch (IOException ex)
            {
                // 当流被关闭时，会抛出此异常，是正常断开流程的一部分
                Debug.Log("连接已由本地或远程主机关闭: " + ex.Message);
            }
            catch (Exception ex)
            {
                // 其他意外异常
                Debug.LogWarning("接收数据异常: " + ex.Message);
            }
            finally
            {
                // 确保在线程退出时执行断开逻辑
                if (IsConnected)
                    UnityMainThreadDispatcher.Instance().Enqueue(Disconnect);
            }
        }

        /// <summary>
        /// 发送消息到服务器
        /// </summary>
        /// <param name="message">要直接发送的信息</param>
        public void SendToServer(string message)
        {
            if (!IsConnected || stream == null) return;
            byte[] data = Encoding.UTF8.GetBytes(message);
            SendToServer(data, message);
        }

        /// <summary>
        /// 发送原始字节数据到服务器
        /// </summary>
        /// <param name="data">发送的原始字节数据</param>
        /// <param name="debugRemark">日志输出信息，不需要时可不填</param>
        public void SendToServer(byte[] data,string debugRemark = "")
        {
            if (!IsConnected || stream == null) return;
            try
            {
                // 1. 准备长度包头
                byte[] lengthPrefix = BitConverter.GetBytes(data.Length);
                // 2. 发送包头和数据
                stream.Write(lengthPrefix, 0, lengthPrefix.Length);
                stream.Write(data, 0, data.Length);
                // 调试日志输出
                debugRemark = string.IsNullOrEmpty(debugRemark) ? "" : $"[{debugRemark}]";
                Debug.Log($"已向服务器发送数据{debugRemark}({data.Length})");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("发送数据失败: " + ex.Message);
            }
        }

        #region 模拟测试
#if UNITY_EDITOR
        [Header("模拟接收")]
        [Tooltip("用于测试的发送目标IP地址")]
        public string receivedMessage;

        [ContextMenu("模拟接收")]
        public void SimulateReceive() => OnReceivedMessage?.Invoke(receivedMessage);

        [Header("模拟发送")]
        [Tooltip("用于测试的发送消息内容")]
        public string sendMessage = "模拟发送消息";

        [ContextMenu("模拟发送")]
        public void SimulateSend() => SendToServer(sendMessage);
#endif 
        #endregion
    }
}