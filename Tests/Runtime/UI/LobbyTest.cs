using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using UnityEngine.UI;

namespace DreemurrStudio.Network.DEMO
{
    /// <summary>
    /// 控制大厅测试场景中所有UI的更新与交互逻辑。
    /// 这是一个纯粹的UI表现层脚本，它响应LobbyManager的事件来更新UI，并将用户的UI输入转发给LobbyManager。
    /// </summary>
    public class LobbyTest : MonoBehaviour
    {
        /// <summary>
        /// 定义了UI所处的不同状态
        /// </summary>
        public enum State
        {
            /// <summary>
            /// 在主菜单
            /// </summary>
            MainMenu,
            /// <summary>
            /// 在大厅（房间列表）
            /// </summary>
            InLobby,
            /// <summary>
            /// 在一个具体的房间内
            /// </summary>
            InRoom
        }

        [Header("主菜单界面")]
        [SerializeField]
        [Tooltip("主菜单面板的根GameObject")]
        private GameObject mainMenuPanelGO;
        [SerializeField]
        [Tooltip("IP地址下拉选择菜单")]
        private Dropdown ipAddressDropdown;
        [SerializeField]
        [Tooltip("用于过滤本地IP地址的子网前缀")]
        private string ipSubnetFilter = "192.168.";
        [SerializeField]
        [Tooltip("端口号输入框")]
        private InputField portInput;
        [SerializeField]
        [Tooltip("玩家昵称输入框")]
        private InputField playerNameInput;
        [SerializeField]
        [Tooltip("房间名输入框")]
        private InputField roomNameInput;

        [Header("大厅界面")]
        [SerializeField]
        [Tooltip("大厅面板的根GameObject")]
        private GameObject lobbyPanelGO;
        [SerializeField]
        [Tooltip("房间列表容器的RectTransform")]
        private RectTransform roomItemCollectionRT;
        [SerializeField]
        [Tooltip("显示房间数量的文本")]
        private Text roomNumText;
        [SerializeField]
        [Tooltip("房间列表项预制体")]
        private RoomItem roomItemPrefab;

        [Header("房间界面")]
        [SerializeField]
        [Tooltip("房间面板的根GameObject")]
        private GameObject roomPanelGO;
        [SerializeField]
        [Tooltip("当前房间信息的UI项")]
        private RoomItem currentRoomItem;
        [SerializeField]
        [Tooltip("玩家信息列表容器的RectTransform")]
        private RectTransform playerInfoItemCollection;
        [SerializeField]
        [Tooltip("玩家信息列表项预制体")]
        private PlayerInfoItem playerInfoItemPrefab;
        [SerializeField]
        [Tooltip("聊天消息列表容器的RectTransform")]
        private RectTransform talkMessageItemCollection;
        [SerializeField]
        [Tooltip("聊天消息列表项预制体")]
        private TalkMessageItem talkMessageItemPrefab;
        [SerializeField]
        [Tooltip("聊天消息列表的最大消息数量")]
        private int maxTalkMessageItemNum = 50;
        [SerializeField]
        [Tooltip("聊天输入框")]
        private InputField talkInputField;
        [Header("调试")]
        [SerializeField]
        [Tooltip("用于测试的，进行强制连接的IP地址")]
        private string testIPAdress;
        [SerializeField]
        [Tooltip("用于测试的，进行强制连接的端口号")]
        private int testPort;

        /// <summary>
        /// 获取或设置UI输入框中的IP和端口号。
        /// </summary>
        public IPEndPoint IPEP
        {
            get
            {
                // 修改：从Dropdown获取IP
                string selectedIP = ipAddressDropdown.options[ipAddressDropdown.value].text;

                // 检查输入有效性
                if (IPAddress.TryParse(selectedIP, out IPAddress ip) && int.TryParse(portInput.text, out int port))
                    return new IPEndPoint(ip, port);
                // 返回一个默认或无效值，或者抛出异常
                Debug.LogError("IP地址或端口号格式无效！");
                return null;
            }
        }

        /// <summary>
        /// 获取或设置UI输入框中的端口号。
        /// </summary>
        public int Port
        {
            get => int.Parse(portInput.text);
            set => portInput.text = value.ToString();
        }

        /// <summary>
        /// 获取或设置UI输入框中的玩家昵称。
        /// </summary>
        public string PlayerName
        {
            get => playerNameInput.text;
            set => playerNameInput.text = value;
        }

        /// <summary>
        /// 获取或设置UI输入框中的房间名。
        /// </summary>
        public string RoomName
        {
            get => roomNameInput.text;
            set => roomNameInput.text = value;
        }

        /// <summary>
        /// 当前UI所处的状态
        /// </summary>
        private State currentState;

        private void Start()
        {
            currentState = State.MainMenu;
            mainMenuPanelGO.SetActive(true);
            lobbyPanelGO.SetActive(false);
            roomPanelGO.SetActive(false);

            // 初始化清空已有的UI项
            var ris = roomItemCollectionRT.GetComponentsInChildren<RoomItem>();
            foreach (var ri in ris) Destroy(ri.gameObject);
            var pis = playerInfoItemCollection.GetComponentsInChildren<PlayerInfoItem>();
            foreach (var pi in pis) Destroy(pi.gameObject);
            var tmis = talkMessageItemCollection.GetComponentsInChildren<TalkMessageItem>();
            foreach (var tmi in tmis) Destroy(tmi.gameObject);

            // 新增：填充IP地址下拉菜单
            PopulateIPAddressDropdown();

            // --- 事件订阅 ---
            // 确保LobbyManager实例存在
            if (LobbyManager.Instance == null)
                throw new Exception("场景中缺少 LobbyManager 实例！");
            // 主菜单事件
            LobbyManager.Instance.onRoomHosted += OnRoomHosted;
            // 大厅事件
            LobbyManager.Instance.onLobbyRoomUpdated += OnLobbyRoomUpdated;
            LobbyManager.Instance.onLobbyRoomRemoved += OnLobbyRoomRemoved;
            // 房间事件
            LobbyManager.Instance.onRoomUpdated += OnRoomUpdated;
            LobbyManager.Instance.onPlayerInfoUpdated += OnPlayerInfoUpdated;
            LobbyManager.Instance.onPlayerLeft += OnPlayerLeftRoom;
            LobbyManager.Instance.onLeftRoom += OnLeftRoom;
            LobbyManager.Instance.onPlayerSpeak += OnPlayerSpeak;
            LobbyManager.Instance.onRoomClosed += OnRoomClosed;
        }

        /// <summary>
        /// 填充IP地址下拉菜单的方法
        /// </summary>
        private void PopulateIPAddressDropdown()
        {
            ipAddressDropdown.ClearOptions();
            var ipAddresses = NetworkUtils.GetLocalIPv4Addresses(ipSubnetFilter);
            if (ipAddresses.Count > 0)
                ipAddressDropdown.AddOptions(ipAddresses);
            ipAddressDropdown.AddOptions(new List<string> { "127.0.0.1" });
            ipAddressDropdown.RefreshShownValue();
        }

        private void OnDestroy()
        {
            // --- 事件取消订阅，防止内存泄漏 ---
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.onRoomHosted -= OnRoomHosted;
                LobbyManager.Instance.onLobbyRoomUpdated -= OnLobbyRoomUpdated;
                LobbyManager.Instance.onLobbyRoomRemoved -= OnLobbyRoomRemoved;
                LobbyManager.Instance.onRoomUpdated -= OnRoomUpdated;
                LobbyManager.Instance.onPlayerInfoUpdated -= OnPlayerInfoUpdated;
                LobbyManager.Instance.onPlayerLeft -= OnPlayerLeftRoom;
                LobbyManager.Instance.onLeftRoom -= OnLeftRoom;
                LobbyManager.Instance.onPlayerSpeak -= OnPlayerSpeak;
                LobbyManager.Instance.onRoomClosed -= OnRoomClosed;
            }
        }

        private void OnPlayerLeftRoom(IPEndPoint ipep, PlayerInfo info)
        {
            if (!playerInfoItems.TryGetValue(ipep, out var item))
                return;
            playerInfoItems.Remove(ipep);
            Destroy(item.gameObject);
        }

        #region 主菜单界面
        /// <summary>
        /// “创建房间”按钮点击事件处理。
        /// </summary>
        public void OnStartHostButtonDown()
        {
            // 从UI收集信息
            var roomInfo = new RoomInfo()
            {
                roomName = RoomName,
                hosterName = PlayerName,
                IPEP = IPEP, // 注意：这里的IPEP可能不是最终服务器监听的IPEP
                playerNum = 1
            };
            var playerInfo = new PlayerInfo()
            {
                playerName = PlayerName,
            };
            // 请求LobbyManager创建房间
            LobbyManager.Instance.StartHostRoom(playerInfo, roomInfo);
            // 立即切换到房间界面（作为房主）
            mainMenuPanelGO.SetActive(false);
        }

        /// <summary>
        /// “进入大厅”按钮点击事件处理。
        /// </summary>
        public void OnEnterLobbyButtonDown()
        {
            var playerInfo = new PlayerInfo()
            {
                playerName = PlayerName,
            };
            // 请求LobbyManager进入大厅（开始监听房间广播）
            LobbyManager.Instance.EnterLobby(playerInfo,IPEP.Address.ToString());
            mainMenuPanelGO.SetActive(false);
            DoEnterLobby();
        }

        /// <summary>
        /// 处理返回主菜单的UI逻辑。
        /// </summary>
        private void DoEnterMainMenu()
        {
            mainMenuPanelGO.SetActive(true);
            currentState = State.MainMenu;
        }
        #endregion

        #region 大厅界面
        private Dictionary<IPEndPoint, RoomItem> roomItems = new();

        /// <summary>
        /// 大厅房间列表项中“加入”按钮的点击回调。
        /// </summary>
        /// <param name="roomIPEP">要加入的房间的IP端点。</param>
        public void OnLobbyRoomItemJoinButtonDown(IPEndPoint roomIPEP)
        {
            LobbyManager.Instance.JoinRoom(roomIPEP, IPEP);
        }

        [ContextMenu("加入测试房间")]
        public void TestTryJoinRoom() => LobbyManager.Instance.JoinRoom(roomItems.Keys.ToList()[0], IPEP);

        /// <summary>
        /// 从大厅返回主菜单的按钮点击事件处理。
        /// </summary>
        public void OnReturnMainMenuFromLobbyButtonDown()
        {
            LobbyManager.Instance.ExitLobby();
            lobbyPanelGO.SetActive(false);
            DoEnterMainMenu();
        }

        /// <summary>
        /// 执行进入大厅的UI初始化逻辑。
        /// </summary>
        private void DoEnterLobby()
        {
            lobbyPanelGO.SetActive(true);
            currentState = State.InLobby;
            // 清理上一次的房间列表
            if(roomItems != null && roomItems.Count > 0)
                foreach (var item in roomItems.Values)
                    Destroy(item.gameObject);
            roomItems.Clear();
            roomNumText.text = $"共发现{roomItems.Count}个房间";
        }

        /// <summary>
        /// 当LobbyManager通知一个房间已移除（超时）时调用。
        /// </summary>
        /// <param name="info">被移除的房间信息。</param>
        private void OnLobbyRoomRemoved(RoomInfo info)
        {
            if(roomItems.TryGetValue(info.IPEP, out RoomItem item))
            {
                item.OnJoinRoomButtonDown -= OnLobbyRoomItemJoinButtonDown;
                roomItems.Remove(info.IPEP);
                Destroy(item.gameObject);
            }
            roomNumText.text = $"共发现{roomItems.Count}个房间";
        }

        /// <summary>
        /// 当LobbyManager发现新房间或更新现有房间时调用。
        /// </summary>
        /// <param name="info">更新后的房间信息。</param>
        private void OnLobbyRoomUpdated(RoomInfo info)
        {
            // 如果房间已存在于UI列表中，则更新它
            if(roomItems.TryGetValue(info.IPEP, out RoomItem item))
                item.Info = info;
            else // 否则，创建一个新的UI项
            {
                var newItem = CreateLobbyRoomItem(info);
                newItem.OnJoinRoomButtonDown += OnLobbyRoomItemJoinButtonDown;
                roomItems.Add(info.IPEP, newItem);
            }
            roomNumText.text = $"共发现{roomItems.Count}个房间";
        }

        /// <summary>
        /// 创建并初始化一个新的大厅房间UI项。
        /// </summary>
        /// <param name="info">新列表项要载入的房间信息。</param>
        /// <returns>创建的RoomItem实例。</returns>
        private RoomItem CreateLobbyRoomItem(RoomInfo info)
        {
            var go = Instantiate(roomItemPrefab.gameObject, roomItemCollectionRT);
            var item = go.GetComponent<RoomItem>();
            item.Info = info;
            return item;
        }
        #endregion

        #region 房间界面
        /// <summary>
        /// 存储房间内玩家信息的UI项列表。
        /// </summary>
        private Dictionary<IPEndPoint, PlayerInfoItem> playerInfoItems = new();
        /// <summary>
        /// 存储房间聊天消息的UI项队列。
        /// </summary>
        private Queue<TalkMessageItem> talkMessageItems = new();

        public void OnReturnMainMenuFromRoomButtonDown()
        {
            if(LobbyManager.Instance.IsHoster)LobbyManager.Instance.CloseHostRoom();
            else LobbyManager.Instance.LeaveRoom();
            roomPanelGO.SetActive(false);
            DoEnterMainMenu();
        }

        public void OnSendTalkButtonDown()
        {
            var words = talkInputField.text;
            if (!string.IsNullOrEmpty(words))
            {
                LobbyManager.Instance.Speak(words);
                talkInputField.text = string.Empty;
            }
        }

        /// <summary>
        /// 当作为房主成功创建房间时调用。
        /// </summary>
        /// <param name="info">房间信息。</param>
        private void OnRoomHosted(RoomInfo info)
        {
            DoEnterRoom(info);
            currentRoomItem.Info = info;
            talkInputField.text = string.Empty;
            // 将房主自己添加到玩家列表中
            var playerInfo = new PlayerInfo() { playerName = PlayerName };
            var item = CreatePlayerInfoItem(playerInfo, info.IPEP.ToString() , true, true);
            playerInfoItems.Add(info.IPEP, item);
        }

        /// <summary>
        /// 当所在的房间被房主关闭时调用。
        /// </summary>
        private void OnRoomClosed()
        {
            // TODO: 弹出一个提示框告知玩家房间已关闭
            roomPanelGO.SetActive(false);
            DoEnterMainMenu();
        }


        /// <summary>
        /// 当房间内有玩家发言时调用。
        /// </summary>
        /// <param name="info">发言的玩家信息。</param>
        /// <param name="words">发言内容。</param>
        private void OnPlayerSpeak(PlayerInfo info, string words)
        {
            var item = CreateTalkMessageItem(info, words);
            talkMessageItems.Enqueue(item);
            // 如果消息数量超过上限，则移除最旧的一条
            if(talkMessageItems.Count > maxTalkMessageItemNum)
            {
                var oldItem = talkMessageItems.Dequeue();
                Destroy(oldItem.gameObject);
            }
        }

        /// <summary>
        /// 当玩家离开房间后调用。
        /// </summary>
        private void OnLeftRoom()
        {
            roomPanelGO.SetActive(false);
            DoEnterMainMenu();
        }

        /// <summary>
        /// 当房间内某个玩家的信息更新时调用。
        /// </summary>
        /// <param name="ipep">玩家的IP端点。</param>
        /// <param name="info">玩家的最新信息。</param>
        private void OnPlayerInfoUpdated(IPEndPoint ipep, PlayerInfo info)
        {
            var localIPEP = LobbyManager.Instance.LocalIPEP;
            var hosterIPEP = LobbyManager.Instance.CurrentRoomIPEP;
            // 更新或创建玩家UI项
            if (playerInfoItems.TryGetValue(ipep, out var item))
                item.Init(info.playerName, ipep.ToString(), ipep.Equals(hosterIPEP), ipep.Equals(localIPEP));
            else
            {
                var newItem = CreatePlayerInfoItem(info, ipep.ToString(), ipep.Equals(hosterIPEP), ipep.Equals(localIPEP));
                playerInfoItems.Add(ipep, newItem);
            }
        }

        /// <summary>
        /// 当作为客户端加入房间，或房间状态全量更新时调用。
        /// </summary>
        /// <param name="info">房间的最新信息。</param>
        /// <param name="roomPlayers">房间内所有玩家的完整列表。</param>
        private void OnRoomUpdated(RoomInfo info, Dictionary<IPEndPoint, PlayerInfo> roomPlayers)
        {
            // 如果当前不在房间内，则先执行进入房间的UI逻辑
            if(currentState != State.InRoom) DoEnterRoom(info);

            var localIPEP = LobbyManager.Instance.LocalIPEP;
            // 全量更新玩家列表
            foreach (var pi in roomPlayers)
            {
                if(playerInfoItems.TryGetValue(pi.Key,out var item))
                    item.Init(pi.Value.playerName, pi.Key.ToString(), pi.Key.Equals(info.IPEP), pi.Key.Equals(localIPEP));
                else
                {
                    var newItem = CreatePlayerInfoItem(pi.Value, pi.Key.ToString(), pi.Key.Equals(info.IPEP), pi.Key.Equals(localIPEP));
                    playerInfoItems.Add(pi.Key, newItem);
                }
            }
            // 更新房间信息显示
            currentRoomItem.Info = info;
        }

        /// <summary>
        /// 执行进入房间的UI初始化逻辑。
        /// </summary>
        private void DoEnterRoom(RoomInfo roomInfo)
        {
            lobbyPanelGO.SetActive(false);
            roomPanelGO.SetActive(true);
            currentState = State.InRoom;
            // 清空上一次的玩家列表和聊天记录
            if (playerInfoItems != null && playerInfoItems.Count > 0)
            {
                foreach (var item in playerInfoItems.Values)
                    Destroy(item.gameObject);
            }
            playerInfoItems.Clear();
            if(talkMessageItems != null && talkMessageItems.Count > 0)
            {
                foreach (var item in talkMessageItems)
                    Destroy(item.gameObject);
            }
            talkMessageItems.Clear();
        }

        /// <summary>
        /// 创建并初始化一个新的玩家信息UI项。
        /// </summary>
        private PlayerInfoItem CreatePlayerInfoItem(PlayerInfo info,string ipep,bool isHost,bool isLocal)
        {
            var go = Instantiate(playerInfoItemPrefab.gameObject, playerInfoItemCollection);
            var item = go.GetComponent<PlayerInfoItem>();
            item.Init(info.playerName, ipep, isHost, isLocal);
            return item;
        }

        /// <summary>
        /// 创建并初始化一个新的聊天消息UI项。
        /// </summary>
        private TalkMessageItem CreateTalkMessageItem(PlayerInfo info, string words)
        {
            var go = Instantiate(talkMessageItemPrefab.gameObject, talkMessageItemCollection);
            var item = go.GetComponent<TalkMessageItem>();
            item.Init(info.playerName, words, DateTime.Now.ToString("HH:mm:ss"));
            return item;
        }
        #endregion
    }
}
