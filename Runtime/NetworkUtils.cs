using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace DreemurrStudio.Network
{
    /// <summary>
    /// 提供网络相关的静态辅助方法。
    /// </summary>
    public static class NetworkUtils
    {
        /// <summary>
        /// 获取本机所有可用的IPv4地址。
        /// </summary>
        /// <param name="subnetFilter">可选的子网过滤器，例如 "192.168.1."。如果提供，则只返回以此开头的IP地址。</param>
        /// <returns>一个包含IP地址字符串的列表。</returns>
        public static List<string> GetLocalIPv4Addresses(string subnetFilter = "")
        {
            var ipAddressList = new List<string>();
            try
            {
                // 获取所有网络接口
                foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
                {
                    // 过滤掉不活动的接口和环回接口
                    if (networkInterface.OperationalStatus != OperationalStatus.Up ||
                        networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;

                    // 获取接口的IP属性
                    var ipProperties = networkInterface.GetIPProperties();
                    foreach (UnicastIPAddressInformation ipInfo in ipProperties.UnicastAddresses)
                    {
                        // 只关心IPv4地址
                        if (ipInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            string ipAddress = ipInfo.Address.ToString();
                            // 如果设置了过滤器，则检查IP是否符合
                            if (string.IsNullOrEmpty(subnetFilter) || ipAddress.StartsWith(subnetFilter))
                                ipAddressList.Add(ipAddress);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError("获取本地IP地址时发生错误: " + ex.Message);
            }

            return ipAddressList;
        }
    }
}