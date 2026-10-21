using System;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using WinForms = System.Windows.Forms;

namespace Clipwell
{
    public partial class App : System.Windows.Application
    {
        private Mutex _singleInstance;
        private WinForms.NotifyIcon _tray;
        private HotkeyWindow _host;
        private PopupWindow _popup;
        private SettingsWindow _settingsWindow;
        private readonly HistoryStore _store = new HistoryStore();
        private AppSettings _settings;
        private bool _ignoreNextClipboardChange;
        private IntPtr _targetWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _singleInstance = new Mutex(true, "Clipwell.SingleInstance", out bool created);
            if (!created)
            {
                Shutdown();
                return;
            }

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _settings = AppSettings.Load();
            _store.Load();
            _store.SetLimit(_settings.HistoryLimit);

            _popup = new PopupWindow(_store);
            _popup.ItemChosen += OnItemChosen;
            _popup.ItemPinToggled += item => _store.TogglePin(item);

            _host = new HotkeyWindow();
            _host.ClipboardChanged += OnClipboardChanged;
            _host.HotkeyPressed += TogglePopup;
            bool hotkeyOk = _host.Register(_settings.HotkeyModifiers, _settings.HotkeyKey);
            Log.Write("Старт. Горячая клавиша " + _settings.HotkeyText + " зарегистрирована: " + hotkeyOk);
            if (!hotkeyOk)
            {
                MessageBox.Show("Горячая клавиша " + _settings.HotkeyText + " занята другой программой. "
                    + "Выберите другое сочетание в настройках (значок Clipwell в трее).",
                    "Clipwell", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var menu = new WinForms.ContextMenuStrip();
            menu.Items.Add("Настройки", null, (s, a) => ShowSettings());
            menu.Items.Add("Выход", null, (s, a) => Shutdown());

            _tray = new WinForms.NotifyIcon
            {
                Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath),
                Text = "Clipwell",
                Visible = true,
                ContextMenuStrip = menu
            };
            _tray.MouseClick += (s, a) =>
            {
                if (a.Button == WinForms.MouseButtons.Left)
                {
                    ShowSettings();
                }
            };

            // Обычный запуск (первичная настройка) открывает окно настроек.
            // Автозапуск с флагом --tray сразу уходит в трей.
            if (!e.Args.Contains("--tray"))
            {
                ShowSettings();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _tray?.Dispose();
            _host?.Dispose();
            _singleInstance?.ReleaseMutex();
            base.OnExit(e);
        }

        private void TogglePopup()
        {
            if (_popup.IsVisible)
            {
                _popup.Hide();
                return;
            }

            // Открываем историю только там, где стоит текстовый курсор
            IntPtr foreground = NativeMethods.GetForegroundWindow();
            bool caret = NativeMethods.HasTextCaret(foreground) || CaretDetector.HasEditableFocus();
            Log.Write("Горячая клавиша. Курсор есть: " + caret + ", фокус: " + CaretDetector.Describe());
            if (!caret && !_settings.OpenWithoutCaret)
            {
                return;
            }

            _targetWindow = foreground;
            _popup.ShowNearCursor();
        }

        private void ShowSettings()
        {
            if (_settingsWindow == null)
            {
                _settingsWindow = new SettingsWindow(_settings, _store);
                _settingsWindow.Changed += OnSettingsChanged;
                _settingsWindow.CaptureChanged += OnHotkeyCaptureChanged;
            }
            _settingsWindow.Refresh();
            _settingsWindow.Show();
            _settingsWindow.Activate();
        }

        private void OnSettingsChanged()
        {
            if (!_host.Register(_settings.HotkeyModifiers, _settings.HotkeyKey))
            {
                MessageBox.Show("Это сочетание уже занято другой программой. Оставлено прежнее.",
                    "Clipwell", MessageBoxButton.OK, MessageBoxImage.Warning);
                _settings.HotkeyModifiers = _host.Modifiers;
                _settings.HotkeyKey = _host.Key;
                _host.Register(_host.Modifiers, _host.Key);
            }
            _settings.Save();
            _settingsWindow?.Refresh();
        }

        private void OnHotkeyCaptureChanged(bool capturing)
        {
            if (capturing)
            {
                _host.Suspend();
            }
            else
            {
                _host.Resume();
            }
        }

        private void OnClipboardChanged()
        {
            if (_ignoreNextClipboardChange)
            {
                _ignoreNextClipboardChange = false;
                return;
            }

            var item = ClipboardReader.Read();
            if (item != null)
            {
                _store.Add(item);
            }
        }

        private async void OnItemChosen(ClipItem item)
        {
            _popup.Hide();

            _ignoreNextClipboardChange = true;
            if (!PutIntoClipboard(item))
            {
                _ignoreNextClipboardChange = false;
                return;
            }

            _store.Add(item);

            if (_targetWindow != IntPtr.Zero)
            {
                bool focused = RestoreFocus(_targetWindow);
                Log.Write("Фокус возвращён в окно " + _targetWindow + ": " + focused);
            }
            await Task.Delay(150);
            IntPtr active = NativeMethods.GetForegroundWindow();
            Log.Write("Перед вставкой: активное окно " + active + ", в буфере текст: " + System.Windows.Clipboard.ContainsText());
            NativeMethods.PressCtrlV();
            Log.Write("Нажат Ctrl+V");
        }

        // Возвращает фокус окну, в котором был курсор, чтобы текст попал именно туда
        private static bool RestoreFocus(IntPtr target)
        {
            uint targetThread = NativeMethods.GetWindowThreadProcessId(target, IntPtr.Zero);
            uint currentThread = NativeMethods.GetCurrentThreadId();

            NativeMethods.AttachThreadInput(currentThread, targetThread, true);
            bool result = NativeMethods.SetForegroundWindow(target);
            NativeMethods.AttachThreadInput(currentThread, targetThread, false);
            return result;
        }

        private static bool PutIntoClipboard(ClipItem item)
        {
            try
            {
                if (item.IsText)
                {
                    System.Windows.Clipboard.SetText(item.GetText());
                }
                else
                {
                    var bitmap = new BitmapImage(new Uri(item.ImagePath));
                    System.Windows.Clipboard.SetImage(bitmap);
                }
                return true;
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                return false;
            }
        }
    }
}
