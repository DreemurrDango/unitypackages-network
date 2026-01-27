using System.Collections;
using System.Collections.Generic;
using System;
using System.Net;
using System.Text;
using UnityEngine;

namespace DreemurrStudio.Network
{
    [RequireComponent(typeof(UDPController))]
    public class UDPSearcher : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("监听广播使用的端口号")]
        private int listenPort = 9999;
        [SerializeField]
        [Tooltip("监听广播使用的本地IP，留空表示Any")]
        private string listenIP = "";

        private UDPController controller;

        /// <summary>
        /// 收到广播时的回调：参数为发送方端点与消息内容
        /// </summary>
        public event Action<IPEndPoint, string> onBroadcastReceived;

        private void Awake()
        {
            controller = GetComponent<UDPController>();
        }

        private void OnEnable()
        {
            if (controller == null) return;
            controller.onReceiveUDPData += HandleReceive;
            controller.Open(listenPort, listenIP, true, true);
        }

        private void OnDisable()
        {
            if (controller == null) return;
            controller.onReceiveUDPData -= HandleReceive;
            controller.Close();
        }

        private void HandleReceive(IPEndPoint remoteEndPoint, byte[] data)
        {
            var message = Encoding.UTF8.GetString(data);
            Debug.Log($"接收到广播[{remoteEndPoint}] -> {message}");
            onBroadcastReceived?.Invoke(remoteEndPoint, message);
        }
    }
}