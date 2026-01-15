using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;

namespace DreemurrStudio.Network
{
    /// <summary>
    /// UDP通信端口控制器
    /// </summary>
    public class UDPController : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("进行发送时使用的本地IP地址。留空以监听所有网络接口(Any)。")]
        private string localIP = "";
        [SerializeField]
        [Tooltip("进行发送时使用的本地端口号")]
        private int localPort = 8080;
        [SerializeField]
        [Tooltip("是否允许收发广播消息")]
        private bool enableBroadcast = false;
        [SerializeField]
        [Tooltip("是否在开始时使用预设属性值自动打开UDP端口")]
        private bool openOnAwake = true;

        [Tooltip("收到消息时事件，参数为消息内容")]
        public UnityEvent<string> onReceiveMessage;
        /// <summary>
        /// 收到消息时事件，参数为发送者的IPEndPoint和完整数据内容
        /// </summary>
        public event Action<IPEndPoint, byte[]> onReceiveUDPData;

        /// <summary>
        /// 进行网络连接的核心 UdpClient
        /// </summary>
        private UdpClient udpClient;
        /// <summary>
        /// 用于在关闭时取消异步操作
        /// </summary>
        private CancellationTokenSource cancellationTokenSource;

        /// <summary>
        /// 是否正在接收数据
        /// </summary>
        private bool _inReceiving = false;
        /// <summary>
        /// 本地端点
        /// </summary>
        private IPEndPoint localEndPoint;

        /// <summary>
        /// 获取当前用于接收的本地IP地址
        /// </summary>
        public string LocalIP => localEndPoint?.Address.ToString();
        /// <summary>
        /// 获取当前用于接收的本地端口号
        /// </summary>
        public int LocalPort => localEndPoint?.Port ?? 0;
        /// <summary>
        /// 获取当前用于接收的本地端点
        /// </summary>
        public IPEndPoint LocalEndPoint => localEndPoint;


        public bool InReceiving
        {
            get => _inReceiving;
            set
            {
                if (value == _inReceiving) return;
                _inReceiving = value; // 先设置状态

                if (value)
                {
                    if (udpClient == null)
                    {
                        Debug.LogError("请先打开UDP端口！");
                        _inReceiving = false;
                        return;
                    }
                    // 开始异步监听
                    cancellationTokenSource = new CancellationTokenSource();
                    StartListeningAsync(cancellationTokenSource.Token);
                    Debug.Log($"开始通过{localEndPoint}接收UDP数据");
                }
                else
                {
                    // 停止监听
                    cancellationTokenSource?.Cancel();
                    Debug.Log($"停止通过{localEndPoint}接收UDP数据");
                }
            }
        }

        private void Start()
        {
            if (openOnAwake) Open(localPort, localIP, true, enableBroadcast);
        }

        /// <summary>
        /// 异步接收数据
        /// </summary>
        private async void StartListeningAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 异步等待接收数据
                    // 注意：UdpClient.ReceiveAsync 不支持 CancellationToken，
                    // 如果一直没有收到消息，这里会一直等待，直到 UdpClient 被 Close() 
                    UdpReceiveResult result = await udpClient.ReceiveAsync();
                    Debug.Log("收到UDP数据包");
                    // 如果在等待过程中被取消，则退出
                    if (token.IsCancellationRequested) break;

                    // 1. 创建接收到数据的深拷贝副本，防止后续数据覆盖或被修改
                    byte[] receivedBytes = new byte[result.Buffer.Length];
                    Buffer.BlockCopy(result.Buffer, 0, receivedBytes, 0, result.Buffer.Length);

                    // 2. 触发 byte[] 事件 (Action 判空直接调用)
                    onReceiveUDPData?.Invoke(result.RemoteEndPoint, receivedBytes);

                    // 3. 触发 string 事件 (UnityEvent)
                    // 移除 GetPersistentEventCount 判断，否则代码添加的监听器无法收到消息
                    if (onReceiveMessage != null)
                    {
                        // 始终解码字符串，以便显示日志（调试用）或触发事件
                        // 如果你不希望在没有监听者时产生字符串分配开销，可以使用 Check 
                        // 但 UnityEvent 内部自己会检查是否有监听者，直接 Invoke 是安全的
                        string receivedMessage = Encoding.UTF8.GetString(receivedBytes);

                        Debug.Log($"接收到来自[{result.RemoteEndPoint}]的消息: {receivedMessage}");
                        onReceiveMessage.Invoke(receivedMessage);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
                // 当 UdpClient Close() 时触发，属于正常退出流程
                Debug.Log("UdpClient 已关闭，监听停止。");
            }
            catch (Exception e)
            {
                // 忽略因取消操作导致的异常
                if (!token.IsCancellationRequested)
                {
                    Debug.LogError($"接收UDP数据时发生错误: {e}");
                }
            }
        }


        /// <summary>
        /// 打开端口以供UDP连接
        /// </summary>
        /// <param name="localPort">用于接收的端口号</param>
        /// <param name="localIP">用于接收的本地IP。留空则监听所有网络接口(Any)。</param>
        /// <param name="inReceiving">是否开始接收</param>
        public void Open(int localPort = 8080, string localIP = "", bool inReceiving = true, bool enableBroadcast = false)
        {
            if (udpClient != null) Close();

            this.localIP = localIP;
            this.localPort = localPort;
            try
            {
                // 设置本地端点并创建 UdpClient
                if (string.IsNullOrEmpty(localIP))
                {
                    // 监听所有网络接口
                    udpClient = new UdpClient(localPort);
                }
                else
                {
                    // 仅监听指定的IP地址
                    udpClient = new UdpClient(new IPEndPoint(IPAddress.Parse(localIP), localPort));
                }

                udpClient.EnableBroadcast = enableBroadcast;
                localEndPoint = (IPEndPoint)udpClient.Client.LocalEndPoint;
                Debug.Log($"UDP套接字已绑定至 {localEndPoint}");
                InReceiving = inReceiving;
            }
            catch (Exception e)
            {
                Debug.LogError($"打开UDP端口失败: {e}");
                udpClient = null;
            }
        }

        /// <summary>
        /// 关闭UDP端口
        /// </summary>
        public void Close()
        {
            if (udpClient == null) return;

            InReceiving = false; // 这会触发 CancellationToken 的取消
            udpClient.Close();
            udpClient = null;
            Debug.Log("UDP套接字已关闭");
        }

        /// <summary>
        /// 发送UDP消息
        /// </summary>
        public void SendUDPMessage(string targetIP, int targetPort, string message)
            => SendUDPMessage(new IPEndPoint(IPAddress.Parse(targetIP), targetPort), message);

        /// <summary>
        /// 发送UPD消息
        /// </summary>
        public async void SendUDPMessage(IPEndPoint remoteEndPoint, string message)
        {
            var sendData = Encoding.UTF8.GetBytes(message);
            await SendUDPMessage(remoteEndPoint, sendData, message);
        }

        /// <summary>
        /// 发送字节数组格式的UDP数据
        /// </summary>
        public async void SendUDPMessage(string targetIP, int targetPort, byte[] data, string debugRemake = "")
            => await SendUDPMessage(new IPEndPoint(IPAddress.Parse(targetIP), targetPort), data, debugRemake);

        /// <summary>
        /// 发送字节数组格式的UDP数据
        /// </summary>
        public async System.Threading.Tasks.Task SendUDPMessage(IPEndPoint remoteEndPoint, byte[] data, string debugRemake = "")
        {
            if (udpClient == null)
            {
                Debug.LogError("UDP客户端未初始化，无法发送消息！");
                return;
            }

            debugRemake = string.IsNullOrEmpty(debugRemake) ? "" : $"[{debugRemake}]";
            try
            {
                await udpClient.SendAsync(data, data.Length, remoteEndPoint);
                Debug.Log($"已向[{remoteEndPoint}]发送UDP数据{debugRemake}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"UDP数据{debugRemake}发送失败，请检查网络情况\n异常信息：{e}");
            }
        }

        private void OnApplicationQuit()
        {
            Close();
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
        public void SimulateReceive()
        {
            onReceiveUDPData?.Invoke(new IPEndPoint(IPAddress.Parse(receiveIP), receivePort), Encoding.UTF8.GetBytes(receivedMessage));
            onReceiveMessage?.Invoke(receivedMessage);
        }

        [Header("模拟发送")]
        [Tooltip("用于测试的发送目标IP地址")]
        public string targetIP = "192.168.1.100";
        [Tooltip("用于测试的发送目标端地址")]
        public int targetPort = 8080;
        [Tooltip("用于测试的发送消息内容")]
        public string sendMessage = "模拟发送消息";

        [ContextMenu("模拟发送")]
        public void SimulateSend()
        {
            SendUDPMessage(targetIP, targetPort, sendMessage);
        }
#endif
        #endregion
    }
}
