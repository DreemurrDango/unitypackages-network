using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.UI;

namespace DreemurrStudio.Network.DEMO
{
    public class RoomItem : MonoBehaviour
    {
        [SerializeField]
        private Text roomNameText;
        [SerializeField]
        private Text hosterNameText;
        [SerializeField]
        private Text ipepText;
        [SerializeField]
        private Text playerNum;
        [SerializeField]
        private Button joinButtom;

        public event Action<IPEndPoint> OnJoinRoomButtonDown;

        private RoomInfo info;
        public RoomInfo Info
        {
            get => info;
            set
            {
                roomNameText.text = value.roomName;
                hosterNameText.text = $"房主:{value.hosterName}";
                ipepText.text = $"IP:{value.IPEP}";
                playerNum.text = $"{value.playerNum}名玩家";
                info = value;
            }
        }

        private void Awake()
        {
            if(joinButtom != null)joinButtom.onClick.AddListener(() => OnJoinRoomButtonDown?.Invoke(info.IPEP));
        }
    }
}
