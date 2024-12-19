using TMPro;

namespace Network.Chat.ChatRoomLogic
{
    using UnityEngine;
    using UnityEngine.UI;
    using System.Text;

    public class ChatView : MonoBehaviour
    {
        [SerializeField] private TMP_InputField chatHistoryText;
        [SerializeField] private TMP_InputField messageInputField;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button backButton;
        [SerializeField] private GameObject chatUI;
        private string _nickName = string.Empty;
        private RoomChatManager currentRoomChat;
        
        public void OpenChat(RoomChatManager chatManager)
        {
            _nickName = _nickName.Equals(string.Empty) ? TextUtility.GetRandomNickname() : _nickName; // no time to save TODO
            currentRoomChat = chatManager;
            chatUI.SetActive(true);
            UpdateChatHistory();
        }

        private void CloseChat()
        {
            chatUI.SetActive(false);
        }

        private void Start()
        {
            sendButton.onClick.AddListener(SendMessage);
            backButton.onClick.AddListener(CloseChat);
            EventBus.instance.OnReceiveRoomMessage += OnReceiveRoomMessage;
        }

        private void OnReceiveRoomMessage(string roomId, string nickname, string msg)
        {
            if (currentRoomChat.RoomName.Equals(roomId))
            {
                currentRoomChat.AddMessage(nickname, msg);
                UpdateChatHistory();
            }
        }

        private void SendMessage()
        {
            if (!string.IsNullOrWhiteSpace(messageInputField.text))
            {
                string message = messageInputField.text;
                currentRoomChat.AddMessage(_nickName, message);
                messageInputField.text = "";
                UpdateChatHistory();
                EventBus.instance.OnSendChatMessage?.Invoke(currentRoomChat.RoomName, _nickName, message);
            }
        }

        private void UpdateChatHistory()
        {
            if (currentRoomChat == null) return;

            StringBuilder chatContent = new StringBuilder();
            foreach (var message in currentRoomChat.GetMessages())
            {
                chatContent.AppendLine($"{message.Author}: {message.Message}");
            }

            chatHistoryText.text = chatContent.ToString();
        }
    }

}