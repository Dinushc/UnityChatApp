using DefaultNamespace;

namespace Network
{
    [System.Serializable]
    public class ServerMessage
    {
        public MessageEnum MessageType;
        public string MessageData;
        
        public string RoomId;
        public string Nickname;
        
        public ServerMessage(MessageEnum messageType, string messageData, string roomId = null, string nickname = null)
        {
            MessageType = messageType;
            MessageData = messageData;
            RoomId = roomId;
            Nickname = nickname;
        }
    }

}