using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DreemurrStudio.Network.DEMO
{
    public class TalkMessageItem : MonoBehaviour
    {
        [SerializeField]
        private Text talkerNameText;
        [SerializeField]
        private Text messageText;

        public void Init(string talkerName, string message)
        {
            talkerNameText.text = talkerName;
            messageText.text = message;
        }
    }
}
