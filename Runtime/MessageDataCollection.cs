using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Plugins.DreemurrStudio.Network
{
    #region C/S架构消息模板
    //WORKFLOW: 添加服务器发送给客户端的网络消息的数据结构
    public enum S2CMessageType
    {
        JoinLobby,
        ExitLobby,
    }

    // WORKFLOW: 添加客户端发送给服务器的网络消息的数据结构
    public enum C2SMessageType
    {
        JoinLobby,
        ExitLobby,
    }


    //WORKFLOW: 需要添加服务器发送给客户端的网络消息数据结构时，添加继承自此基类的新数据结构类
    [System.Serializable]
    /// <summary>
    /// 网络消息数据的基类
    /// </summary>
    public class S2CMessageDataBase
    {
        public S2CMessageType messageType;
    }

    //WORKFLOW: 需要添加客户端发送给服务器的网络消息数据结构时，添加继承自此基类的新数据结构类
    /// <summary>
    /// 客户端向服务器发送的网络消息数据的基类
    /// </summary>
    [System.Serializable]
    public class C2SMessageDataBase
    {
        public C2SMessageType messageType;
    }
    #endregion

    /// <summary>
    /// 消息类型枚举
    /// </summary>
    public enum MessageType
    {
        RequestTwoPlayerGame,
        ResponceTwoPlayerGame,
        ExitTwoPlayerGame,
        PlaceBuilding,
        COUNT
    }

    /// <summary>
    /// 消息的基类
    /// </summary>
    [System.Serializable]
    public class MessageDataBase
    {
        public MessageType messageType;
    }

    /// <summary>
    /// 响应双人游戏请求消息数据
    /// </summary>
    [System.Serializable]
    public class ResponceTwoPlayerGameMessageData : MessageDataBase
    {
        public bool doAccept;
    }
}