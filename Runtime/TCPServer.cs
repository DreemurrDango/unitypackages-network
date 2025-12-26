using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace DreemurrStudio.Network
{
    // TODO:添加ID标记
    /// <summary>
    /// 使用TCP协议的服务器端通信脚本
    /// </summary>
    public class TCPServer : Singleton<TCPServer>
    {
        [SerializeField]
        [Tooltip("服务器的端口号")]
        private int port = 8888;
        [SerializeField]
        [Tooltip("服务器绑定的IP地址")]
        private string serverIP = "127.0.0.1";
        [SerializeField]
        [Tooltip("是否在开始时自动启动TCP服务器")]
        private bool startOnStart = true;

        [Tooltip("收到消息时事件，参数为消息内容")]
        public Action<string> OnReceivedMessage;
        [Tooltip("收到数据时事件，参数为发送端IPEndPoint和数据内容")]
        public Action<IPEndPoint, byte[]> OnReceivedData;

        /// <summary>
        /// 是否正在运行TCP服务器
        /// </summary>
        private bool isRunning = false;
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
        private Dictionary<TcpClient, NetworkStream> clients = new Dictionary<TcpClient, NetworkStream>();

        private void Start()
        {
            if (startOnStart) StartServer(serverIP, port);
        }

        /// <summary>
        /// 打开服务器
        /// </summary>
        public void StartServer(string ip,int port) => 
            StartServer(new IPEndPoint(IPAddress.Parse(ip), port));

        /// <summary>
        /// 打开服务器
        /// </summary>
        public void StartServer(IPEndPoint ipED)
        {
            if (isRunning) return;
            isRunning = true;
            try
            {
                listener = new TcpListener(ipED);
                listener.Start();
                // 获取实际绑定的IP和端口
                var ipep = (IPEndPoint)listener.LocalEndpoint;
                serverIP = ipep.Address.ToString();
                port = ipep.Port;
                // 启动监听线程
                listenThread = new Thread(ListenForClients);
                listenThread.IsBackground = true;
                listenThread.Start();
                Debug.Log($"TCP服务器已启动：{ipED}");
            }
            catch (Exception e)
            {
                isRunning = false;
                Debug.LogError($"TCP服务器{ipED}启动失败:{e}");
            }
        }

        /// <summary>
        /// 停止运行服务器
        /// </summary>
        public void StopServer()
        {
            isRunning = false;
            listener?.Stop();
            foreach (var client in clients.Keys)
                if (client != null && client.Connected)client.Close();
            clients.Clear();
            listenThread?.Join();
            Debug.Log($"TCP服务器{serverIP}:{port}已关闭");
        }

        /// <summary>
        /// 监听客户端连接线程程序
        /// </summary>
        private void ListenForClients()
        {
            while (isRunning)
            {
                try
                {
                    if (listener.Pending())
                    {
                        TcpClient client = listener.AcceptTcpClient();
                        NetworkStream stream = client.GetStream();
                        lock (clients)
                        {
                            clients.Add(client, stream);
                        }
                        Debug.Log("客户端已连接: " + client.Client.RemoteEndPoint);
                        var clientThread = new Thread(() => HandleClientComm(client, stream));
                        clientThread.IsBackground = true;
                        clientThread.Start();
                    }
                    Thread.Sleep(10);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("监听异常: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 处理收到的客户端消息
        /// </summary>
        /// <param name="client"></param>
        /// <param name="stream"></param>
        private void HandleClientComm(TcpClient client, NetworkStream stream)
        {
            byte[] buffer = new byte[1024];
            try
            {
                while (isRunning && client.Connected)
                {
                    if (stream.DataAvailable)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break; // 客户端断开
                        OnReceivedData?.Invoke((IPEndPoint)client.Client.RemoteEndPoint, buffer[..bytesRead]);
                        if(OnReceivedMessage == null) continue;
                        string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                        Debug.Log("收到客户端消息: " + message);
                        OnReceivedMessage?.Invoke(message);
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("客户端通信异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 向特定客户端发送消息
        /// </summary>
        /// <param name="client">要发送的客户端目标</param>
        /// <param name="message">要发送的客户端信息</param>
        public void SendToClient(TcpClient client, string message)
        {
            if (client == null || !client.Connected) return;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                NetworkStream stream;
                lock (clients)
                {
                    if (!clients.TryGetValue(client, out stream)) return;
                }
                stream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("发送消息失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 向特定客户端发送原始字节数据
        /// </summary>
        /// <param name="client">要发送的客户端目标</param>
        /// <param name="data">要发送的二进制字节数组数据</param>
        /// <param name="debugRemake">可附加的调试信息</param>
        public void SendToClient(TcpClient client, byte[] data,string debugRemake = "")
        {
            if (client == null || !client.Connected) return;
            try
            {
                NetworkStream stream;
                lock (clients)
                {
                    if (!clients.TryGetValue(client, out stream)) return;
                }
                stream.Write(data, 0, data.Length);
                debugRemake = string.IsNullOrEmpty(debugRemake) ? "" : $"[{debugRemake}]";
                Debug.Log($"已向[{client.Client.RemoteEndPoint}]发送TCP消息 {debugRemake}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("发送消息失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 向所有客户端广播消息
        /// </summary>
        /// <param name="message"></param>
        public void SendToAllClients(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            lock (clients)
            {
                foreach (var kvp in clients)
                {
                    TcpClient client = kvp.Key;
                    NetworkStream stream = kvp.Value;
                    if (client.Connected)
                    {
                        try
                        {
                            stream.Write(data, 0, data.Length);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("广播消息失败: " + ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 向所有客户端广播原始字节数据
        /// </summary>
        /// <param name="data">要发送的原始字节数据</param>
        /// <param name="debugRemake">可附加的调试标记信息</param>
        public void SendToAllClients(byte[] data,string debugRemake = "")
        {
            lock (clients)
            {
                foreach (var kvp in clients)
                {
                    TcpClient client = kvp.Key;
                    NetworkStream stream = kvp.Value;
                    if (client.Connected)
                    {
                        try
                        {
                            stream.Write(data, 0, data.Length);
                            debugRemake = string.IsNullOrEmpty(debugRemake) ? "" : $"[{debugRemake}]";
                            Debug.Log($"已向[{client.Client.RemoteEndPoint}]发送TCP消息 {debugRemake}");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("广播消息失败: " + ex.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// MonoBehaviour生命周期管理
        /// </summary>
        private void OnApplicationQuit()
        {
            StopServer();
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
        public void SimulateReceive() => OnReceivedMessage?.Invoke(receivedMessage);

        [Header("模拟发送")]
        [Tooltip("用于测试的发送消息内容")]
        public string sendMessage = "模拟发送消息";

        [ContextMenu("模拟发送")]
        public void SimulateSend() => SendToAllClients(sendMessage);
#endif 
        #endregion
    }
}