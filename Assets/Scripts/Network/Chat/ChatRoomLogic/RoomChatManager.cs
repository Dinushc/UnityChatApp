namespace Network.Chat.ChatRoomLogic
{
    using System.Collections.Generic;

    public class RoomChatManager
    {
        public string RoomName { get; private set; }
        private List<string> users = new();
        private List<ChatMessage> messages = new();

        public RoomChatManager(string roomName)
        {
            RoomName = roomName;
        }

        public void AddUser(string userName)
        {
            if (!users.Contains(userName))
            {
                users.Add(userName);
            }
        }

        public void RemoveUser(string userName)
        {
            users.Remove(userName);
        }

        public void AddMessage(string author, string message)
        {
            messages.Add(new ChatMessage(author, message));
        }

        public List<ChatMessage> GetMessages()
        {
            return new List<ChatMessage>(messages);
        }

        public List<string> GetUsers()
        {
            return new List<string>(users);
        }
    }
}