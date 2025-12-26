using System;
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
        /// 连接到服务器
        /// </summary>
        /// <param name="serverIPEP">要连接到的服务器IP端点</param>
        /// <param name="clientIPEP">本地客户端IP端点</param>
        public bool ConnectToServer(IPEndPoint serverIPEP,IPEndPoint clientIPEP)
        {
            if (IsConnected) return true;
            try
            {
                client = clientIPEP != null ? new TcpClient() : new TcpClient(clientIPEP);
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
            if(!IsConnected)return;
            client?.Close();
            stream?.Close();
            receiveThread?.Join();
            Debug.Log("已断开与服务器的连接");
        }

        /// <summary>
        /// 接收服务器消息
        /// </summary>
        private void ListenningMessage()
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (IsConnected)
                {
                    if (stream.DataAvailable)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break; // 服务器断开
                        OnReceivedData?.Invoke(buffer[0..bytesRead]);
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Debug.Log("收到服务器消息: " + message);
                        OnReceivedMessage?.Invoke(message);
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("接收数据异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 发送消息到服务器
        /// </summary>
        /// <param name="message">要直接发送的信息</param>
        public void SendMessageToServer(string message)
        {
            if (!IsConnected || stream == null) return;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("向服务器发送消息失败: " + ex.Message);
            }
        }

        public void SendDataToServer(byte[] data,string debugRemark = "")
        {
            if (!IsConnected || stream == null) return;
            debugRemark = string.IsNullOrEmpty(debugRemark) ? "" : $"[{debugRemark}]";
            try
            {
                stream.Write(data, 0, data.Length);
                Debug.Log($"已向服务器发送数据{debugRemark}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("发送数据失败: " + ex.Message);
            }
        }

        private void Start()
        {
            if (connectOnStart) 
                ConnectToServer(new IPEndPoint(IPAddress.Parse(serverIP), serverPort), autoGetClientIPEP ? null : new IPEndPoint(IPAddress.Parse(clientIP), clientPort));
        }

        private void OnApplicationQuit()
        {
            Disconnect();
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
        public void SimulateSend() => SendMessageToServer(sendMessage);
#endif 
        #endregion
    }
}