using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace DreemurrStudio.Network
{
    [RequireComponent(typeof(UDPController))]
    /// <summary>
    /// UDP广播者：基于udpController封装实现广播自身地址，进行配对
    /// </summary>
    public class UDPBroadcaster : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("使用的IP子网过滤器")]
        private string ipSubnetFilter = "192.168.";
        [SerializeField]
        [Tooltip("广播使用的端口号")]
        private int port = 9999;
        [SerializeField]
        [Tooltip("广播间隔，单位秒")]
        private float broadcastInterval = 1.0f;
        [SerializeField]
        [Tooltip("要在发送时附带的消息内容")]
        private string message = "UDP BROADCASTING";

        /// <summary>
        /// 用于执行UDP广播的控制器
        /// </summary>
        private UDPController controller;
        /// <summary>
        /// 持续进行广播的协程引用
        /// </summary>
        private Coroutine broadcastCoroutine = null;

        /// <summary>
        /// 获取当前是否正处于广播中
        /// </summary>
        public bool IsBroadcasting
        {
            get => controller != null && enabled;
            set => enabled = value;
        }

        private void Awake()
        {
            controller = GetComponent<UDPController>();
        }

        public void OnEnable()
        {
            var localIPs = NetworkUtils.GetLocalIPv4Addresses(ipSubnetFilter);
            if (localIPs.Count > 0)
            {
                string ipToBroadcast = localIPs[0];
                if (!string.IsNullOrEmpty(message)) message = ipToBroadcast;
                broadcastCoroutine = StartCoroutine(BroadcastCoroutine(ipToBroadcast, port, message));
                Debug.Log($"开始通过{ipToBroadcast}:{port}广播消息:{message}");
            }
            else Debug.LogWarning($"{ipSubnetFilter}下没有可用的IP地址");
        }

        public void OnDisable()
        {
            if (broadcastCoroutine != null)
            {
                StopCoroutine(broadcastCoroutine);
                broadcastCoroutine = null;
                controller.Close();
            }
        }

        /// <summary>
        /// 设置要用于进行广播的参数
        /// </summary>
        /// <param name="ipSubnetFilter">IP子网筛选</param>
        /// <param name="port">要进行广播的端口号</param>
        /// <param name="broadcastInterval">广播时间间隔</param>
        /// <param name="message">要广播的额外消息内容</param>
        public void Set(string ipSubnetFilter, int port, float broadcastInterval, string message)
        {
            this.ipSubnetFilter = ipSubnetFilter;
            this.port = port;
            this.broadcastInterval = broadcastInterval;
            this.message = message;
        }


        /// <summary>
        /// 持续间断性地广播消息
        /// </summary>
        /// <param name="ipAdress">要进行广播的IP地址</param>
        /// <param name="port">进行广播的端口号</param>
        /// <param name="message">要广播的消息内容</param>
        /// <returns></returns>
        private IEnumerator BroadcastCoroutine(string ipAdress, int port, string message)
        {
            controller.Open(port, ipAdress, false, true);
            while (true)
            {
                controller.BroadcastUDPMessage(message, port);
                yield return new WaitForSeconds(broadcastInterval);
            }
        }
    }
}
