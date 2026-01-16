using DreemurrStudio.Utilities;
using System;
using System.Net;
using UnityEngine;
using UnityEngine.UI;

namespace DreemurrStudio.Network.DEMO
{
    /// <summary>
    /// 贴图传送测试的客户端脚本
    /// </summary>
    public class TextureTransmitTestClient : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("要发送的纹理")]
        private Texture2D sendTexture;
        [SerializeField]
        [Tooltip("显示发送/接收到的纹理的图片组件")]
        private RawImage showTextureImage;
        [SerializeField]
        [Tooltip("客户端端口")]
        private TCPClient client;

        /// <summary>
        /// Unity主线程调度器
        /// </summary>
        private UnityMainThreadDispatcher mainThreadDispatcher;

        private void Start()
        {
            client.OnReceivedRawData += OnDataReceived;
            mainThreadDispatcher = UnityMainThreadDispatcher.Instance();
        }

        /// <summary>
        /// 受到图像数据时的回调
        /// </summary>
        /// <param name="imageData">图形的二进制数据</param>
        private void OnDataReceived(byte[] imageData)
        {
            Debug.Log($"收到来自服务器的图像数据，大小:{imageData.Length}bytes");

            // 使用 UnityMainThreadDispatcher 将 Unity API 调用调度到主线程
            mainThreadDispatcher.Enqueue(() =>
            {
                var tex = new Texture2D(2, 2);
                // LoadImage会根据数据自动调整纹理大小
                if (tex.LoadImage(imageData))
                {
                    showTextureImage.texture = tex;
                    Debug.Log("图片加载成功！");
                }
                else Debug.LogWarning("收到的数据无法解析为图片。");
            });
        }

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.Button("发送纹理")]
#else
        [ContextMenu("发送纹理")]
#endif
        public void SendTexture()
        {
            if (sendTexture == null)
            {
                Debug.LogWarning("未指定要发送的纹理");
                return;
            }
            showTextureImage.texture = sendTexture;
            byte[] textureData = sendTexture.EncodeToPNG();
            client.SendToServer(textureData, true,sendTexture.name);
        }
    }
}
