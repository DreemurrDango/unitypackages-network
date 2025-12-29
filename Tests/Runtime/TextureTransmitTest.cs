using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.UI;
using DreemurrStudio.Utilities;

namespace DreemurrStudio.Network.DEMO
{
    /// <summary>
    /// 网络通信模块测试纹理网络传输的管理者脚本
    /// </summary>
    public class TextureTransmitTest : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("要发送的图片")]
        private Texture2D imageToSend;
        [SerializeField]
        [Tooltip("用于显示源发送的图片的UI组件")]
        private RawImage sourceImageDisplay;
        [SerializeField]
        [Tooltip("用于显示接收到的图片的UI组件")]
        private RawImage receivedImageDisplay;
        [SerializeField]
        [Tooltip("客户端TCP通信代理")]
        private TCPClient client;
        [SerializeField]
        [Tooltip("服务器TCP通信代理")]
        private TCPServer server;

        private void Start()
        {
            client.OnReceivedData += (data) => OnImageReceived(server.ServerIPEP, data);
            server.OnReceivedData += (ipep, data) => OnImageReceived(ipep, data);
        }

        [ContextMenu("C2S发送图片")]
        public void C2S_SendImage()
        {
            sourceImageDisplay.texture = imageToSend;
            if (client.IsConnected)
            {
                byte[] imageData = imageToSend.EncodeToPNG();
                client.SendToServer(imageData, $"PNG Image{imageToSend.name}");
            }
            else Debug.Log("客户端未连接到服务器，发送失败");
        }

        [ContextMenu("S2C广播图片")]
        public void S2C_SendImage()
        {
            sourceImageDisplay.texture = imageToSend;
            if (server.ConnectedNum > 0)
            {
                var imageData = imageToSend.EncodeToPNG();
                server.SendToAllClients(imageData, $"已广播PNG图片{imageToSend.name}给所有客户端({server.ConnectedNum}个)");
            }
            else Debug.Log("服务器未连接任何客户端，发送失败");
        }

        // 你也可以为服务器添加类似的发送方法，例如 SendImageToAllClients()

        private void OnImageReceived(IPEndPoint ipep,byte[] imageData)
        {
            Debug.Log($"收到来自{ipep}的图像数据，大小:{imageData.Length}bytes");

            // 使用 UnityMainThreadDispatcher 将 Unity API 调用调度到主线程
            UnityMainThreadDispatcher.Instance().Enqueue(() =>
            {
                var tex = new Texture2D(2, 2);
                // LoadImage会根据数据自动调整纹理大小
                if (tex.LoadImage(imageData))
                {
                    receivedImageDisplay.texture = tex;
                    Debug.Log("图片加载成功！");
                }
                else Debug.LogWarning("收到的数据无法解析为图片。");
            });
        }

        private void OnDestroy()
        {
            client.OnReceivedData -= (data) => OnImageReceived(server.ServerIPEP, data);
            server.OnReceivedData -= (ipep, data) => OnImageReceived(ipep, data);
        }
    }
}
