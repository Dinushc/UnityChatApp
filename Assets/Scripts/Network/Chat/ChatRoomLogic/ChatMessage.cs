namespace Network.Chat.ChatRoomLogic
{
    public class ChatMessage
    {
        public string Author { get; private set; }
        public string Message { get; private set; }

        public ChatMessage(string author, string message)
        {
            Author = author;
            Message = message;
        }
    }
}