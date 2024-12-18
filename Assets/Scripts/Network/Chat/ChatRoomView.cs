using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Network.Chat
{
    public class ChatRoomView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _roomIPLabel;
        [SerializeField] private Button _enterButton;
        [SerializeField] private Button _closeButton;

        public TextMeshProUGUI roomLabel => _roomIPLabel;
        public void SetEnterButtonListener(System.Action onClickAction)
        {
            _enterButton.onClick.RemoveAllListeners();
            _enterButton.onClick.AddListener(() => onClickAction?.Invoke());
        }

        public void SetCloseButtonListener(System.Action onClickAction)
        {
            _closeButton.onClick.RemoveAllListeners();
            _closeButton.onClick.AddListener(() => onClickAction?.Invoke());
        }
    
        public void UpdateRoomLabel(string ip)
        {
            _roomIPLabel.text = ip;
        }
    
        private void OnDestroy()
        {
            _enterButton.onClick.RemoveAllListeners();
            _closeButton.onClick.RemoveAllListeners();
        }
    }
}