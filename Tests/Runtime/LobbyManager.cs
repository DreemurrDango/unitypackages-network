using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Serialization;
using Newtonsoft.Json;
using UnityEngine;

namespace DreemurrStudio.Network.DEMO
{
    //[System.Serializable]
    //public struct IPID
    //{
    //    public string ip;
    //    public int port;

    //    /// <summary>
    //    /// 获取或设置房主的TCP端点。
    //    /// 这个属性作为辅助，不会被序列化。
    //    /// </summary>
    //    [JsonIgnore]
    //    public IPEndPoint IPEP
    //    {
    //        get => new IPEndPoint(IPAddress.Parse(ip), port);
    //        set
    //        {
    //            ip = value.Address.ToString();
    //            port = value.Port;
    //        }
    //    }

    //    public IPID(string ip,int port) { this.ip = ip;this.port = port; }
    //    public IPID(IPEndPoint ipep) { this.ip = ipep.Address.ToString();this.port = ipep.Port; }

    //    /// <summary>
    //    /// 判断与指定IPEndPoint是否相等
    //    /// </summary>
    //    /// <param name="ipep"></param>
    //    /// <returns></returns>
    //    public bool Equals(IPEndPoint ipep) => ipep.Address.ToString() == ip && ipep.Port == port;

    //    public override string ToString() => $"{ip}:{port}";
    //}

    /// <summary>
    /// 房间列表信息结构体
    /// </summary>
    [System.Serializable]
    public class RoomInfo
    {
        public string roomName;
        public string hosterName;

        //public IPID hostIPID;
        public string hosterIP;
        public int hosterPort;

        public int playerNum;
        //public int maxPlayer;

        [JsonIgnore] // 告诉Json.NET不要序列化这个字段
        public float t_lastUpdateTime; // 用于超时检测

        /// <summary>
        /// 获取或设置房主的TCP端点。
        /// 这个属性作为辅助，不会被序列化。
        /// </summary>
        [JsonIgnore]
        public IPEndPoint IPEP
        {
            //get => hostIPID.IPEP;
            //set => hostIPID.IPEP = value;
            get => new IPEndPoint(IPAddress.Parse(hosterIP), hosterPort);
            set
            {
                hosterIP = value.Address.ToString();
                hosterPort = value.Port;
            }
        }

        public string ToJson() => JsonConvert.SerializeObject(this);
        public static RoomInfo FromJson(string json) => JsonConvert.DeserializeObject<RoomInfo>(json);
    }

    [System.Serializable]
    public struct PlayerInfo
    {
        public string playerName;
    }

    /// <summary>
    /// 在游戏大厅中的状态
    /// </summary>
    public enum LobbyState
    {
        NONE,
        Hosting,
        InLobby,
        Joining,
        Joined
    }

    /// <summary>
    /// 游戏大厅管理器
    /// </summary>
    public class LobbyManager : Singleton<LobbyManager>
    {
        [Header("组件引用")]
        [SerializeField] 
        private UDPController udpBroadcaster;
        [SerializeField] 
        private TCPServer tcpServer;
        [SerializeField] 
        private TCPClient tcpClient;

        [Header("大厅设置")]
        [SerializeField]
        [Tooltip("用于UDP收发广播消息的端口号")]
        private int broadcastPort = 9999;
        [SerializeField]
        [Tooltip("UDP广播间隔（秒）")]
        private float broadcastInterval = 1.0f;
        [SerializeField]
        [Tooltip("房间超时时间（秒）")]
        private float roomTimeout = 5.0f;

        /// <summary>
        /// 存储发现的房间信息，Key是房主的UDP端点
        /// </summary>
        private Dictionary<IPEndPoint, RoomInfo> discoveredRooms;
        /// <summary>
        /// 当前所在房间内的玩家信息列表
        /// </summary>
        private Dictionary<IPEndPoint, PlayerInfo> roomPlayers;
        /// <summary>
        /// 本地玩家信息
        /// </summary>
        private PlayerInfo localPlayerInfo;

        /// <summary>
        /// 当前的大厅状态
        /// </summary>
        private LobbyState lobbyState = LobbyState.NONE;
        /// <summary>
        /// 上次广播的时间
        /// </summary>
        private float t_lastBroadcast;
        /// <summary>
        /// 当前所在房间的信息
        /// </summary>
        private RoomInfo currrentRoomInfo;

        /// <summary>
        /// 获取当前是否在大厅中
        /// </summary>
        public bool IsInLobby => lobbyState == LobbyState.InLobby || lobbyState == LobbyState.Hosting;
        /// <summary>
        /// 获取当前大厅状态
        /// </summary>
        public LobbyState CurrentLobbyState => lobbyState;
        /// <summary>
        /// 本地玩家信息
        /// </summary>
        public PlayerInfo LocalPlayerInfo => localPlayerInfo;
        /// <summary>
        /// 当前所在房间的信息
        /// </summary>
        public RoomInfo CurrentRoomInfo => currrentRoomInfo;
        /// <summary>
        /// 本地TCP客户端的IP端点
        /// </summary>
        public IPEndPoint LocalIPEP => lobbyState switch
        { 
            LobbyState.Hosting => tcpServer.ServerIPEP,
            LobbyState.Joined => tcpClient.ClientIPEP,
            _ => null
        };
        /// <summary>
        /// 当前所在房间的房主TCP端点
        /// </summary>
        public IPEndPoint CurrentRoomIPEP => currrentRoomInfo.IPEP;
        /// <summary>
        /// 获取当前是否为房主
        /// </summary>
        public bool IsHoster => lobbyState == LobbyState.Hosting;
        /// <summary>
        /// 获取只读的已发现的房间列表
        /// </summary>
        public IReadOnlyDictionary<IPEndPoint, RoomInfo> DiscoveredRooms => discoveredRooms;
        /// <summary>
        /// 获取只读的当前房间内玩家列表
        /// </summary>
        public IReadOnlyDictionary<IPEndPoint, PlayerInfo> RoomPlayers => roomPlayers;

        // 游客事件
        /// <summary>
        /// 大厅中有房间更新时的回调
        /// </summary>
        public event Action<RoomInfo> onLobbyRoomUpdated;
        /// <summary>
        /// 大厅中有房间移除时的回调
        /// </summary>
        public event Action<RoomInfo> onLobbyRoomRemoved;
        /// <summary>
        /// 作为游客所在房间有信息更新时的回调
        /// 参数为<当前房间信息,<IP字符串,玩家信息字典>>
        /// </summary>
        public event Action<RoomInfo, Dictionary<IPEndPoint, PlayerInfo>> onRoomUpdated;
        /// <summary>
        /// 作为游客离开房间时的回调
        /// </summary>
        public event Action onLeftRoom;

        // 房主事件
        /// <summary>
        /// 房主成功主持房间时的回调
        /// </summary>
        public event Action<RoomInfo> onRoomHosted;
        /// <summary>
        /// 作为房主主持房间时，有玩家信息更新的回调
        /// </summary>
        public event Action<IPEndPoint,PlayerInfo> onPlayerInfoUpdated;
        /// <summary>
        /// 作为房主主持房间时，有玩家离开的回调
        /// </summary>
        public event Action<IPEndPoint, PlayerInfo> onPlayerLeft;
        /// <summary>
        /// 作为房主关闭房间时的回调
        /// </summary>
        public event Action onRoomClosed;

        // 房间内事件
        /// <summary>
        /// 房间内有玩家发言时的回调
        /// </summary>
        public event Action<PlayerInfo, string> onPlayerSpeak;

        private void Start()
        {
            tcpClient.OnConnectedToServer += OnTCPClientConnectedToServer;
            tcpClient.OnDisconnectedFromServer += OnTCPClientDisconnectedFromServer;
        }


        #region 作为游客
        public void EnterLobby(PlayerInfo playerInfo,string ipAddress)
        {
            if (lobbyState == LobbyState.InLobby) return;
            localPlayerInfo = playerInfo;
            DoEnterLobby(ipAddress);
        }

        public void ExitLobby()
        {
            if (lobbyState == LobbyState.InLobby) DoExitLobby();
        }

        public void JoinRoom(IPEndPoint hostIPEP,IPEndPoint clientIPEP)
        {
            if (lobbyState == LobbyState.Joining || tcpClient.IsConnected) return;
            DoJoinRoom(hostIPEP, clientIPEP);
        }

        public void Speak(string words)
        {
            if (lobbyState == LobbyState.Joined) SendTCPMessage_PlayerSpeak(words);
            else if (lobbyState == LobbyState.Hosting)
            {
                SendTCPMessage_PlayerSpeak(LocalIPEP, words);
                onPlayerSpeak?.Invoke(localPlayerInfo, words);
            }
        }

        public void LeaveRoom()
        {
            if (lobbyState != LobbyState.Joined || !tcpClient.IsConnected) return;
            DoLeaveRoom();
        }

        /// <summary>
        /// 定期清理超时房间的协程引用
        /// </summary>
        Coroutine timeoutRoomCleanCoroutine = null;
        /// <summary>
        /// 游客进入大厅
        /// </summary>
        private void DoEnterLobby(string ipAddress)
        {
            discoveredRooms = new Dictionary<IPEndPoint, RoomInfo>();
            // 监听网络消息
            udpBroadcaster.Open(broadcastPort, ipAddress, true, true);
            udpBroadcaster.onReceiveMessage.AddListener(OnReceiveBroadcast);
        }
        #region 发送消息
        private void SendTCPMessage_PlayerInfoUpdated(PlayerInfo playerInfo)
        {
            var message = new C2S_PlayerInfoUpdatedMessage
            {
                messageType = LobbyMessageType.C2S_PlayerInfoUpdated,
                playerInfo = playerInfo
            };
            var json = JsonConvert.SerializeObject(message);
            tcpClient.SendToServer(json);
        }
        private void SendTCPMessage_PlayerSpeak(string words)
        {
            var message = new C2S_PlayerSpeakMessage
            {
                messageType = LobbyMessageType.C2S_PlayerSpeak,
                words = words
            };
            var json = JsonConvert.SerializeObject(message);
            tcpClient.SendToServer(json);
        }
        #endregion

        /// <summary>
        /// 大厅房间更新协程
        /// </summary>
        /// <returns></returns>
        private IEnumerator CleanupTimeoutRoomsCoroutine()
        {
            while (lobbyState == LobbyState.InLobby)
            {
                // ToList()创建副本以安全地修改字典
                foreach (var roomKey in discoveredRooms.Keys.ToList())
                {
                    var info = discoveredRooms[roomKey];
                    if (Time.time > info.t_lastUpdateTime + roomTimeout)
                    {
                        Debug.Log($"房间 '{discoveredRooms[roomKey].roomName}' 超时，已移除");
                        discoveredRooms.Remove(roomKey);
                        onLobbyRoomRemoved?.Invoke(info);
                    }
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        /// <summary>
        /// 收到UDP广播包时的回调
        /// </summary>
        private void OnReceiveBroadcast(string message)
        {
            try
            {
                var roomInfo = RoomInfo.FromJson(message);
                roomInfo.t_lastUpdateTime = Time.time;
                // 使用发送者的IP和房间指定的TCP端口来更新或添加房间信息
                discoveredRooms[roomInfo.IPEP] = roomInfo;
                Debug.Log($"发现或更新房间: {roomInfo.roomName}({roomInfo.IPEP})");
                onLobbyRoomUpdated?.Invoke(roomInfo);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"处理UDP广播失败: {e.Message}");
            }
        }

        /// <summary>
        /// 加入一个房间
        /// </summary>
        private void DoJoinRoom(IPEndPoint hostIPEP,IPEndPoint clientIPEP)
        {
            Debug.Log($"正在尝试使用{clientIPEP}加入{hostIPEP}");
            lobbyState = LobbyState.Joining;
            tcpClient.ConnectToServer(hostIPEP, clientIPEP);
        }

        /// <summary>
        /// TCP客户端成功连接到服务器时的回调
        /// </summary>
        /// <param name="clientIPEP"></param>
        /// <param name="serverIPEP"></param>
        private void OnTCPClientConnectedToServer(IPEndPoint clientIPEP, IPEndPoint serverIPEP)
        {
            DoExitLobby();
            tcpClient.OnReceivedMessage += OnClientReceiveRoomMessage;
            SendTCPMessage_PlayerInfoUpdated(localPlayerInfo);
            lobbyState = LobbyState.Joined;
        }

        /// <summary>
        /// 作为游客收到房间内消息时的回调
        /// </summary>
        /// <param name="message"></param>
        private void OnClientReceiveRoomMessage(string message)
        {
            var data = JsonConvert.DeserializeObject<LobbyMessageDataBase>(message);
            switch (data.messageType)
            {
                case LobbyMessageType.S2C_PlayerInfoUpdated:
                    var infoData = JsonConvert.DeserializeObject<S2C_PlayerInfoUpdateMessage>(message);
                    onRoomUpdated?.Invoke(infoData.roomInfo, infoData.PlayerInfos);
                    break;
                case LobbyMessageType.S2C_PlayerSpeak:
                    var speakData = JsonConvert.DeserializeObject<S2C_PlayerSpeakMessage>(message);
                    onPlayerSpeak?.Invoke(speakData.speakerInfo, speakData.words);
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 执行离开房间操作
        /// </summary>
        private void DoLeaveRoom()
        {
            Debug.Log($"离开房间{tcpClient.ServerIPEP}");
            tcpClient.Disconnect();
        }

        /// <summary>
        /// 处理TCP客户端断开连接时的回调
        /// </summary>
        /// <param name="clientIPEP">断开时的客户端IP端点</param>
        /// <param name="serverIPEP">断开的服务器IP端点</param>
        private void OnTCPClientDisconnectedFromServer(IPEndPoint clientIPEP, IPEndPoint serverIPEP)
        {
            lobbyState = LobbyState.NONE;
            onLeftRoom?.Invoke();
        }

        /// <summary>
        /// 游客退出大厅
        /// </summary>
        private void DoExitLobby()
        {
            if(lobbyState != LobbyState.InLobby) return;
            StopCoroutine(timeoutRoomCleanCoroutine);
            timeoutRoomCleanCoroutine = null;
            // 取消监听
            udpBroadcaster.Close();
            udpBroadcaster.onReceiveMessage.RemoveListener(OnReceiveBroadcast);
            lobbyState = LobbyState.NONE;
        }
        #endregion

        #region 作为房主
        public void StartHostRoom(PlayerInfo playerInfo,RoomInfo roomInfo)
        {
            if (lobbyState == LobbyState.Hosting) return;
            localPlayerInfo = playerInfo;
            DoHostRoom(roomInfo);
        }

        public void CloseHostRoom()
        {
            if (lobbyState != LobbyState.Hosting) return;
            DoCloseHost();
        }

        /// <summary>
        /// 持续广播所主持房间信息的协程引用
        /// </summary>
        private Coroutine broadcastHostingRoomCoroutine = null;
        /// <summary>
        /// 主持一个新房间
        /// </summary>
        /// <param name="roomInfo">房间信息</param>
        private void DoHostRoom(RoomInfo roomInfo)
        {
            if (lobbyState == LobbyState.Hosting) return;
            // 1. 启动TCP服务器，端口号设为0时，系统会自动分配一个可用端口
            udpBroadcaster.Open(broadcastPort,roomInfo.hosterIP, false, true);
            tcpServer.StartServer(roomInfo.IPEP);
            tcpServer.OnReceivedMessage += OnServerReceiveRoomMessage;
            roomPlayers = new Dictionary<IPEndPoint, PlayerInfo>();
            Debug.Log($"房间 '{roomInfo.roomName}' 已创建，TCP服务运行于: {tcpServer.ServerIPEP}");
            currrentRoomInfo = roomInfo;
            currrentRoomInfo.IPEP = tcpServer.ServerIPEP;
            lobbyState = LobbyState.Hosting;
            onRoomHosted?.Invoke(roomInfo);
            // 开始广播房间信息协程
            broadcastHostingRoomCoroutine = StartCoroutine(BroadcastHostingRoomCoroutine());
        }

        /// <summary>
        /// 持续广播所主持房间信息的协程
        /// </summary>
        /// <returns></returns>
        private IEnumerator BroadcastHostingRoomCoroutine()
        {
            // 立即进行一次广播
            DoBroadcastRoomInfo(currrentRoomInfo);
            t_lastBroadcast = Time.time;
            while (lobbyState == LobbyState.Hosting)
            {
                if (Time.time > t_lastBroadcast + broadcastInterval)
                {
                    DoBroadcastRoomInfo(currrentRoomInfo);
                    t_lastBroadcast = Time.time;
                }
                yield return null;
            }
        }

        /// <summary>
        /// 接收到房间内消息时的回调
        /// </summary>
        /// <param name="message"></param>
        private void OnServerReceiveRoomMessage(IPEndPoint ipep,string message)
        {
            if(lobbyState != LobbyState.Hosting) return;
            // 处理来自客户端的消息
            var baseData = JsonConvert.DeserializeObject<LobbyMessageDataBase>(message);
            switch (baseData.messageType)
            {
                case LobbyMessageType.C2S_PlayerInfoUpdated:
                    var infoData = JsonConvert.DeserializeObject<C2S_PlayerInfoUpdatedMessage>(message);
                    roomPlayers[ipep] = infoData.playerInfo;
                    currrentRoomInfo.playerNum = roomPlayers.Count;
                    onPlayerInfoUpdated?.Invoke(ipep, infoData.playerInfo);
                    SendTCPMessage_PlayerInfoUpdated(roomPlayers);
                    break;
                case LobbyMessageType.C2S_PlayerSpeak:
                    var speakData = JsonConvert.DeserializeObject<C2S_PlayerSpeakMessage>(message);
                    SendTCPMessage_PlayerSpeak(ipep, speakData.words);
                    onPlayerSpeak?.Invoke(roomPlayers[ipep], speakData.words);
                    break;
                case LobbyMessageType.COUNT:
                    break;
                default:
                    break;
            }
        }

        /// <summary>
        /// 停止当前所主持房间
        /// </summary>
        private void DoCloseHost()
        {
            if (lobbyState != LobbyState.Hosting) return;
            StopCoroutine(broadcastHostingRoomCoroutine);
            broadcastHostingRoomCoroutine = null;
            tcpServer.StopServer();
            tcpServer.OnReceivedMessage -= OnServerReceiveRoomMessage;
            udpBroadcaster.Close();
            Debug.Log($"房间 '{currrentRoomInfo.roomName}' 已关闭");
            lobbyState = LobbyState.NONE;
            onRoomClosed?.Invoke();
        }

        /// <summary>
        /// 广播房间信息
        /// </summary>
        private void DoBroadcastRoomInfo(RoomInfo roomInfo)
        {
            var data = roomInfo.ToJson();
            udpBroadcaster.SendUDPMessage(IPAddress.Broadcast.ToString(), broadcastPort, data);
        }

        #region TCP消息发送
        /// <summary>
        /// 向所有客户端发送玩家信息更新的TCP消息
        /// </summary>
        /// <param name="playerInfos">所有更新的玩家信息</param>
        private void SendTCPMessage_PlayerInfoUpdated(Dictionary<IPEndPoint, PlayerInfo> playerInfos)
        {
            var message = new S2C_PlayerInfoUpdateMessage
            {
                messageType = LobbyMessageType.S2C_PlayerInfoUpdated,
                roomInfo = currrentRoomInfo,
                PlayerInfos = playerInfos
            };
            var json = JsonConvert.SerializeObject(message);
            tcpServer.SendToAllClients(json);
        }

        /// <summary>
        /// 向所有客户端发送玩家发言的TCP消息
        /// </summary>
        /// <param name="speakerIPEP"></param>
        /// <param name="words"></param>
        private void SendTCPMessage_PlayerSpeak(IPEndPoint speakerIPEP, string words)
        {
            if (!roomPlayers.TryGetValue(speakerIPEP, out var info)) return;
            var message = new S2C_PlayerSpeakMessage
            {
                messageType = LobbyMessageType.S2C_PlayerSpeak,
                speakerInfo = info,
                words = words
            };
            var json = JsonConvert.SerializeObject(message);
            tcpServer.SendToAllClients(json);
        }
        #endregion
        #endregion
    }
}