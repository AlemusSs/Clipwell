using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinForms = System.Windows.Forms;

namespace Clipwell
{
    public partial class PopupWindow : Window
    {
        private const double CornerRadius = 8;

        private readonly HistoryStore _store;

        public event Action<ClipItem> ItemChosen;
        public event Action<ClipItem> ItemPinToggled;

        public PopupWindow(HistoryStore store)
        {
            InitializeComponent();
            _store = store;
            SizeChanged += (s, e) => ApplyRegion();
            PinnedList.PreviewMouseWheel += InvertWheel;
            HistoryList.PreviewMouseWheel += InvertWheel;
        }

        public void ShowNearCursor()
        {
            RefreshList();

            Show();
            UpdateLayout();
            PositionNearCursor();
            ApplyRegion();

            if (HistoryList.Items.Count > 0)
            {
                HistoryList.ScrollIntoView(HistoryList.Items[0]);
            }
            Activate();
            HistoryList.Focus();
        }

        private void ApplyRegion()
        {
            NativeMethods.ApplyBlockRegion(this, new FrameworkElement[] { PinnedBlock, HistoryBlock }, CornerRadius);
        }

        // Закреплённые — отдельным блоком сверху, остальные — в основном окне
        private void RefreshList()
        {
            var pinned = _store.Items.Where(i => i.Pinned).ToList();
            PinnedList.ItemsSource = pinned;
            PinnedBlock.Visibility = pinned.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            HistoryList.ItemsSource = _store.Items.Where(i => !i.Pinned).ToList();
        }

        private void PositionNearCursor()
        {
            NativeMethods.GetCursorPos(out var pt);

            double scale = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11 ?? 1;
            double cursorX = pt.X / scale;
            double cursorY = pt.Y / scale;

            var area = WinForms.Screen.FromPoint(new System.Drawing.Point(pt.X, pt.Y)).WorkingArea;
            double areaLeft = area.Left / scale;
            double areaTop = area.Top / scale;
            double areaRight = area.Right / scale;
            double areaBottom = area.Bottom / scale;

            double x = cursorX;
            double y = cursorY;
            if (x + ActualWidth > areaRight) x = areaRight - ActualWidth;
            if (y + ActualHeight > areaBottom) y = cursorY - ActualHeight;
            if (x < areaLeft) x = areaLeft;
            if (y < areaTop) y = areaTop;

            Left = x;
            Top = y;
        }

        // Инвертированная прокрутка колёсиком: вращение вверх двигает список вниз
        private static void InvertWheel(object sender, MouseWheelEventArgs e)
        {
            var list = (ListBox)sender;
            var scrollViewer = FindScrollViewer(list);
            if (scrollViewer == null)
            {
                return;
            }
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + e.Delta / 3.0);
            e.Handled = true;
        }

        private static ScrollViewer FindScrollViewer(DependencyObject parent)
        {
            if (parent is ScrollViewer viewer)
            {
                return viewer;
            }
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var found = FindScrollViewer(System.Windows.Media.VisualTreeHelper.GetChild(parent, i));
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void List_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var list = (ListBox)sender;
            if (list.SelectedItem is ClipItem item)
            {
                list.SelectedItem = null;
                Log.Write("Выбран элемент: " + (item.IsText ? "текст" : "картинка"));
                ItemChosen?.Invoke(item);
            }
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            if (((Button)sender).DataContext is ClipItem item)
            {
                ItemPinToggled?.Invoke(item);
                RefreshList();
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            Hide();
        }
    }
}
