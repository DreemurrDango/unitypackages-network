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
        [SerializeField]
        private Text timeText;

        public void Init(string talkerName, string message,string time)
        {
            talkerNameText.text = talkerName;
            messageText.text = message;
            timeText.text = time;
        }
    }
}
