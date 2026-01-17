using Newtonsoft.Json;
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
        public static bool TryParseIPEP(string ipepStr, out IPEndPoint resultIPEP)
        {
            var parts = ipepStr.Split(':');
            resultIPEP = null;
            if (parts.Length != 2) return false;
            if (!IPAddress.TryParse(parts[0], out var ip)) return false;
            if (!int.TryParse(parts[1], out var port)) return false;
            resultIPEP = new IPEndPoint(ip, port);
            return true;
        }

        public static IPEndPoint ParseIPEP(string ipepStr)
        {
            if (TryParseIPEP(ipepStr, out var resultIPEP)) 
                return resultIPEP; 
            throw new System.FormatException($"Invalid IPEndPoint string: {ipepStr}");
        }
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
        [JsonProperty]
        protected Dictionary<string,PlayerInfo> playerInfos;


        [JsonIgnore]
        public Dictionary<IPEndPoint, PlayerInfo> PlayerInfos
        {
            get
            {
                var dict = new Dictionary<IPEndPoint, PlayerInfo>();
                foreach (var kv in playerInfos)
                    dict.Add(ParseIPEP(kv.Key), kv.Value);
                return dict;
            }
            set
            {
                playerInfos = new Dictionary<string, PlayerInfo>();
                foreach (var kv in value)
                    playerInfos.Add(kv.Key.ToString(), kv.Value);
            }
        }
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
