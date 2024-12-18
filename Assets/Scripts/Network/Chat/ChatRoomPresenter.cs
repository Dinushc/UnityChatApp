using System;

namespace Network.Chat
{
    public class ChatRoomPresenter
    {
        public Action<string> OnEnterToRoom;
        public Action<ChatRoomView> OnCloseRoom;
        private ChatRoomView _view;
        private string _label;

        public ChatRoomPresenter(ChatRoomView view)
        {
            _view = view;
        
            _view.SetEnterButtonListener(OnEnter);
            _view.SetCloseButtonListener(OnClose);
        }
    
        private void OnEnter()
        {
            OnEnterToRoom?.Invoke(_label);
        }

        private void OnClose()
        {
            OnCloseRoom?.Invoke(_view);
        }
    
        public void SetRoomLabel(string label)
        {
            _label = label;
            _view.UpdateRoomLabel(_label);
        }
    }
}