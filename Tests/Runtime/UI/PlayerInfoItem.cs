using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;
using UnityEngine.UI;

namespace DreemurrStudio.Network.DEMO
{
    public class PlayerInfoItem : MonoBehaviour
    {
        [SerializeField]
        private Text playerNameText;
        [SerializeField]
        private Text playerIPEPText;
        [SerializeField]
        private GameObject hostFlagGO;
        [SerializeField]
        private GameObject localFlagGO;

        public void Init(string playerName, string playerIPEP, bool isHost, bool isLocal)
        {
            playerNameText.text = playerName;
            playerIPEPText.text = playerIPEP;
            hostFlagGO.SetActive(isHost);
            localFlagGO.SetActive(isLocal);
        }
    }
}
