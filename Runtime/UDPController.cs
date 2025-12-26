using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEditor.VersionControl;
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
        [Tooltip("进行发送时使用的本地IP地址")]
        private string localIP = "127.0.0.1";
        [SerializeField]
        [Tooltip("进行发送时使用的本地端口号")]
        private int localPort = 8080;
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
        /// 进行网络连接
        /// </summary>
        private Socket socket;
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
        public string LocalIP => localIP;
        /// <summary>
        /// 获取当前用于接收的本地端口号
        /// </summary>
        public int LocalPort => localPort;
        /// <summary>
        /// 获取当前用于接收的本地端点
        /// </summary>
        public IPEndPoint LocalEndPoint => localEndPoint;


        public bool InReceiving
        {
            get => _inReceiving;
            set
            {
                if(value == _inReceiving) return;
                if(value)
                {
                    if (socket == null)
                    {
                        Debug.LogError("请先打开UDP端口！");
                        return;
                    }
                    StartCoroutine(ReceiveDataCoroutine());
                    Debug.Log("开始接收UDP数据");
                }
                else
                {
                    StopAllCoroutines();
                    Debug.Log("停止接收UDP数据");
                }
                _inReceiving = value;
            }
        }

        private void Start()
        {
            if (openOnAwake) Open(localIP, localPort, true);
        }

        /// <summary>
        /// 接收数据协程
        /// </summary>
        /// <returns></returns>
        private IEnumerator ReceiveDataCoroutine()
        {
            // 创建缓冲区
            byte[] buffer = new byte[1024];
            EndPoint senderEndPoint = new IPEndPoint(IPAddress.Any, 0);
            while (true)
            {
                if (!socket.Poll(1000, SelectMode.SelectRead))
                    yield return null;
                else
                {
                    try
                    {
                        // 接收数据
                        int receivedBytes = socket.ReceiveFrom(buffer, ref senderEndPoint);
                        var iped = (IPEndPoint)senderEndPoint;
                        onReceiveUDPData?.Invoke(iped, buffer[0..receivedBytes]);
                        if(onReceiveMessage != null)
                        {
                            var receivedMessage = Encoding.UTF8.GetString(buffer, 0, receivedBytes);
                            Debug.Log($"接收到来自[{iped}]的消息: {receivedMessage}");
                            onReceiveMessage?.Invoke(receivedMessage);
                        }                        
                    }
                    catch (Exception e) { Debug.LogWarning(e); };
                }
            }
        }

        /// <summary>
        /// 打开端口以供UDP连接
        /// </summary>
        /// <param name="localIP">用于接收的本地IP</param>
        /// <param name="localPort">用于接收的端口号</param>
        /// <param name="inReceiving">是否开始接收</param>
        public void Open(string localIP, int localPort = 8080,bool inReceiving = true)
        {
            this.localIP = localIP;
            this.localPort = localPort;
            // 创建UDP套接字
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            // 设置本地端点
            localEndPoint = new IPEndPoint(IPAddress.Parse(localIP), localPort);
            socket.Bind(localEndPoint);
            Debug.Log($"UDP套接字已绑定至 {localEndPoint}");
            InReceiving = inReceiving;
        }

        /// <summary>
        /// 关闭UDP端口
        /// </summary>
        public void Close()
        {
            if (socket == null) return;
            socket.Close();
            Debug.Log("UDP套接字已关闭");
        }

        /// <summary>
        /// 发送UDP消息
        /// </summary>
        /// <param name="targetIP">发送目标IP地址</param>
        /// <param name="targetPort">发送目标端口号</param>
        /// <param name="message">要发送的消息</param>
        public void SendUDPMessage(string targetIP, int targetPort, string message)
            => SendUDPMessage(new IPEndPoint(IPAddress.Parse(targetIP), targetPort), message);

        /// <summary>
        /// 发送UPD消息
        /// </summary>
        /// <param name="remoteEndPoint">目标IP标的</param>
        /// <param name="message">发送的消息内容</param>
        public void SendUDPMessage(IPEndPoint remoteEndPoint, string message)
        {
            var sendData = Encoding.UTF8.GetBytes(message);
            try
            {
                socket.SendTo(sendData, remoteEndPoint);
                Debug.Log($" 已向[{remoteEndPoint}]发送UDP消息：{message}");
            }
            catch 
            {
                Debug.LogWarning($"{remoteEndPoint}不在线！UDP消息发送失败，请检查网络情况");
            }
        }

        /// <summary>
        /// 发送16位数组格式的UDP数据，一般用来发送二进制数据
        /// </summary>
        /// <param name="targetIP">发送目标IP地址</param>
        /// <param name="targetPort">发送目标端口号</param>
        /// <param name="data">要发送的字节数据</param>
        /// <param name="debugRemake">可附带的调试信息</param>
        public void SendUDPMessage(string targetIP, int targetPort, byte[] data,string debugRemake = "")
            => SendUDPMessage(new IPEndPoint(IPAddress.Parse(targetIP), targetPort), data,debugRemake);

        /// <summary>
        /// 发送16位数组格式的UDP数据，一般用来发送二进制数据
        /// </summary>
        /// <param name="remoteEndPoint">目标IP标的</param>
        /// <param name="data">要发送的字节数据</param>
        /// <param name="debugRemake">可附带的调试信息</param>
        public void SendUDPMessage(IPEndPoint remoteEndPoint, byte[] data,string debugRemake = "")
        {
            debugRemake = string.IsNullOrEmpty(debugRemake) ? "" : $"[{debugRemake}]";
            try
            {
                socket.SendTo(data, remoteEndPoint);
                Debug.Log($" 已向[{remoteEndPoint}]发送UDP数据{debugRemake}");
            }
            catch
            {
                Debug.LogWarning($"UDP数据{debugRemake}发送失败，请检查网络情况");
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
