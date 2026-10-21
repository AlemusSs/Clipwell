using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace Clipwell
{
    public partial class SettingsWindow : Window
    {
        private const double CornerRadius = 8;

        private readonly AppSettings _settings;
        private readonly HistoryStore _store;
        private bool _capturing;

        // Изменились настройки: приложение должно их применить и сохранить
        public event Action Changed;

        // Начало и конец выбора горячей клавиши: на это время клавиша отключается
        public event Action<bool> CaptureChanged;

        public SettingsWindow(AppSettings settings, HistoryStore store)
        {
            InitializeComponent();
            _settings = settings;
            _store = store;
            SizeChanged += (s, e) => NativeMethods.ApplyBlockRegion(this, new FrameworkElement[] { Panel }, CornerRadius);
            Refresh();
        }

        public void Refresh()
        {
            HotkeyText.Text = _settings.HotkeyText;
            AutostartBox.IsChecked = Autostart.IsEnabled();
            HiddenBox.IsChecked = _settings.StartHidden;
            HiddenBox.IsEnabled = AutostartBox.IsChecked == true;
            AlwaysOpenBox.IsChecked = _settings.OpenWithoutCaret;
            LimitText.Text = _settings.HistoryLimit.ToString();
        }

        private void AlwaysOpenBox_Click(object sender, RoutedEventArgs e)
        {
            _settings.OpenWithoutCaret = AlwaysOpenBox.IsChecked == true;
            Changed?.Invoke();
        }

        private void HotkeyButton_Click(object sender, RoutedEventArgs e)
        {
            _capturing = true;
            HotkeyText.Text = "Нажмите сочетание";
            CaptureChanged?.Invoke(true);
            Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_capturing)
            {
                if (e.Key == Key.Escape)
                {
                    Hide();
                    e.Handled = true;
                }
                return;
            }

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (IsModifierKey(key))
            {
                return;
            }

            e.Handled = true;

            if (key == Key.Escape)
            {
                StopCapture();
                Refresh();
                return;
            }

            uint modifiers = 0;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) modifiers |= NativeMethods.MOD_CONTROL;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) modifiers |= NativeMethods.MOD_SHIFT;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) modifiers |= NativeMethods.MOD_ALT;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) modifiers |= NativeMethods.MOD_WIN;

            if (modifiers == 0)
            {
                // Одна буква без Ctrl, Shift или Alt сломает обычный ввод, такое сочетание не берём
                HotkeyText.Text = "Нужен Ctrl, Shift или Alt";
                return;
            }

            StopCapture();
            _settings.HotkeyModifiers = modifiers;
            _settings.HotkeyKey = (uint)KeyInterop.VirtualKeyFromKey(key);
            Changed?.Invoke();
        }

        private void StopCapture()
        {
            _capturing = false;
            CaptureChanged?.Invoke(false);
        }

        private static bool IsModifierKey(Key key)
        {
            return key == Key.LeftCtrl || key == Key.RightCtrl
                || key == Key.LeftShift || key == Key.RightShift
                || key == Key.LeftAlt || key == Key.RightAlt
                || key == Key.LWin || key == Key.RWin;
        }

        private void AutostartBox_Click(object sender, RoutedEventArgs e)
        {
            bool enabled = AutostartBox.IsChecked == true;
            Autostart.Set(enabled, _settings.StartHidden);
            HiddenBox.IsEnabled = enabled;
            Changed?.Invoke();
        }

        private void HiddenBox_Click(object sender, RoutedEventArgs e)
        {
            _settings.StartHidden = HiddenBox.IsChecked == true;
            if (AutostartBox.IsChecked == true)
            {
                Autostart.Set(true, _settings.StartHidden);
            }
            Changed?.Invoke();
        }

        private void Minus_Click(object sender, RoutedEventArgs e)
        {
            SetLimit(_settings.HistoryLimit - 5);
        }

        private void Plus_Click(object sender, RoutedEventArgs e)
        {
            SetLimit(_settings.HistoryLimit + 5);
        }

        private void SetLimit(int value)
        {
            if (value < AppSettings.MinLimit || value > AppSettings.MaxLimit)
            {
                return;
            }
            _settings.HistoryLimit = value;
            _store.SetLimit(value);
            Changed?.Invoke();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            var answer = MessageBox.Show(this, "Очистить историю? Это нельзя отменить.", "Clipwell",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Yes)
            {
                _store.Clear();
            }
        }

        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            e.Cancel = true;
            if (_capturing)
            {
                StopCapture();
            }
            Hide();
        }
    }
}
