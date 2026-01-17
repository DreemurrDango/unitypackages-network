using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

namespace DreemurrStudio.Network.DEMO
{
    public enum LobbyMessageType
    {
        C2S_PlayerInfoUpdated,
        S2C_PlayerInfoUpdated,
        C2S_PlayerSpeak,
        S2C_PlayerSpeak,
        COUNT
    }

    /// <summary>
    /// 消息的基类
    /// </summary>
    [System.Serializable]
    public class LobbyMessageDataBase
    {
        public LobbyMessageType messageType;
    }


    [System.Serializable]
    public class C2S_PlayerInfoUpdatedMessage : LobbyMessageDataBase
    {
        public PlayerInfo playerInfo;
    }

    [System.Serializable]
    public class S2C_PlayerInfoUpdateMessage : LobbyMessageDataBase
    {
        public RoomInfo roomInfo;
        public Dictionary<IPID,PlayerInfo> playerInfos;
    }

    [System.Serializable]
    public class C2S_PlayerSpeakMessage : LobbyMessageDataBase
    {
        public string words;
    }

    [System.Serializable]
    public class S2C_PlayerSpeakMessage : LobbyMessageDataBase
    {
        public PlayerInfo speakerInfo;
        public string words;
    }
}
