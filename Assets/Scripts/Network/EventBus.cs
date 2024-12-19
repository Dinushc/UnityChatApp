using System;
using UnityEngine;

namespace Network
{
    public class EventBus : MonoBehaviour
    {
        private static readonly Lazy<EventBus> _instance = new(() => new EventBus());
        
        private EventBus()
        {
            
        }

        public static EventBus instance => _instance.Value;

        public Action<string, string, string> OnSendChatMessage;
        public Action<string, string, string> OnReceiveRoomMessage;
        public Action<string> OnCloseChat;
    }
}