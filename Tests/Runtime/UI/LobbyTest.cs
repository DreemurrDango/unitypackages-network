using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.UI;

namespace DreemurrStudio.Network.DEMO
{
    /// <summary>
    /// 控制测试场景中所有UI逻辑
    /// </summary>
    public class LobbyTest : MonoBehaviour
    {
        public enum State
        {
            MainMenu,
            InLobby,
            InRoom
        }

        [Header("主菜单界面")]
        [SerializeField]
        private GameObject mainMenuPanelGO;
        [SerializeField]
        private InputField ipAddressInput;
        [SerializeField]
        private InputField portInput;
        [SerializeField]
        private InputField playerNameInput;
        [SerializeField]
        private InputField roomNameInput;

        [Header("大厅界面")]
        [SerializeField]
        private GameObject lobbyPanelGO;
        [SerializeField]
        private RectTransform roomItemCollectionRT;
        [SerializeField]
        private Text roomNumText;
        [SerializeField]
        private RoomItem roomItemPrefab;

        [Header("房间界面")]
        [SerializeField]
        private GameObject roomPanelGO;
        [SerializeField]
        private RoomItem currentRoomItem;
        [SerializeField]
        private PlayerInfoItem playerInfoItemPrefab;
        [SerializeField]
        private RectTransform playerInfoItemCollection;
        [SerializeField]
        private TalkMessageItem talkMessageItemPrefab;
        [SerializeField]
        private RectTransform talkMessageItemCollection;
        
        public IPEndPoint IPEP
        {
            get => new IPEndPoint(IPAddress.Parse(ipAddressInput.text), int.Parse(portInput.text));
            set
            {
                ipAddressInput.text = value.Address.ToString();
                portInput.text = value.Port.ToString();
            }
        }
        public int Port
        {
            get => int.Parse(portInput.text);
            set => portInput.text = value.ToString();
        }
        public string PlayerName
        {
            get => playerNameInput.text;
            set => playerNameInput.text = value;
        }
        public string RoomName
        {
            get => roomNameInput.text;
            set => roomNameInput.text = value;
        }

        private State currentState;

        private void Awake()
        {
            
        }

        private void Start()
        {
            currentState = State.MainMenu;
            mainMenuPanelGO.SetActive(true);
            lobbyPanelGO.SetActive(false);
            roomPanelGO.SetActive(false);
        }


        #region 主菜单界面
        public void OnStartHostButtonDown()
        {
            var roomInfo = new RoomInfo()
            {
                roomName = RoomName,
                hosterName = PlayerName,
                hostTCPEndPoint = IPEP,
                playerNum = 1
            };
            var playerInfo = new PlayerInfo()
            {
                playerName = PlayerName,
            };
            LobbyManager.Instance.StartHostRoom(playerInfo, roomInfo);
            mainMenuPanelGO.SetActive(false);
            roomPanelGO.SetActive(true);
            currentState = State.InRoom;
        }

        public void OnEnterLobbyButtonDown()
        {
            var playerInfo = new PlayerInfo()
            {
                playerName = PlayerName,
            };
            LobbyManager.Instance.EnterLobby(playerInfo);
            mainMenuPanelGO.SetActive(false);
            lobbyPanelGO.SetActive(true);
            currentState = State.InLobby;
        }
        #endregion

        #region 大厅界面
        private Dictionary<IPEndPoint, RoomItem> roomItems = new Dictionary<IPEndPoint, RoomItem>();

        private void DoEnterLobby()
        {
            lobbyPanelGO.SetActive(true);
            currentState = State.InLobby;
            //TODO: 完成进入大厅后的逻辑
        }
        #endregion
    }
}
