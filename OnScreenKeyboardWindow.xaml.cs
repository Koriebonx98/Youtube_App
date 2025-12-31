using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;

namespace youtube_pc_app
{
    public partial class OnScreenKeyboardWindow : Window
    {
        private readonly string[] keys = new string[]
        {
            "Q","W","E","R","T","Y","U","I","O","P",
            "A","S","D","F","G","H","J","K","L","<",
            "Z","X","C","V","B","N","M","_"," ",">",
            "1","2","3","4","5","6","7","8","9","0"
        };
        private List<Button> keyButtons = new List<Button>();
        private int selectedIndex = 0;
        public event Action<string> KeyPressed;
        public event Action CloseRequested;

        public OnScreenKeyboardWindow()
        {
            InitializeComponent();
            BuildKeyboard();
            HighlightKey();
        }

        private void BuildKeyboard()
        {
            KeyGrid.Children.Clear();
            keyButtons.Clear();
            foreach (var key in keys)
            {
                var btn = new Button { Content = key, FontSize = 24, Margin = new Thickness(2) };
                btn.Click += (s, e) => OnKeyClicked(key);
                keyButtons.Add(btn);
                KeyGrid.Children.Add(btn);
            }
        }

        public void MoveSelection(int delta)
        {
            int newIndex = selectedIndex + delta;
            if (newIndex < 0) newIndex = 0;
            if (newIndex >= keyButtons.Count) newIndex = keyButtons.Count - 1;
            selectedIndex = newIndex;
            HighlightKey();
        }

        public void SelectKey()
        {
            OnKeyClicked(keys[selectedIndex]);
        }

        private void HighlightKey()
        {
            for (int i = 0; i < keyButtons.Count; i++)
            {
                keyButtons[i].Background = (i == selectedIndex) ? Brushes.LightBlue : Brushes.White;
            }
        }

        private void OnKeyClicked(string key)
        {
            if (key == ">")
                CloseRequested?.Invoke();
            else
                KeyPressed?.Invoke(key);
        }
    }
}
