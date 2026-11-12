using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;
using LiveCaptionsTranslator.Utils;

namespace LiveCaptionsTranslator
{
    public partial class CaptionPage : Page
    {
        public const int CARD_HEIGHT = 110;

        public static readonly DependencyProperty TranslatedFontSizeProperty = DependencyProperty.Register(
            nameof(TranslatedFontSize),
            typeof(double),
            typeof(CaptionPage),
            new PropertyMetadata(18.0));

        public double TranslatedFontSize
        {
            get => (double)GetValue(TranslatedFontSizeProperty);
            set => SetValue(TranslatedFontSizeProperty, value);
        }

        private static CaptionPage instance;
        public static CaptionPage Instance => instance;

        public CaptionPage()
        {
            InitializeComponent();
            DataContext = Translator.Caption;
            instance = this;

            Loaded += (s, e) =>
            {
                AutoHeight();
                if (App.Current.MainWindow is MainWindow mainWindow)
                    mainWindow.CaptionLogButton.Visibility = Visibility.Visible;
            };
            Unloaded += (s, e) =>
            {
                if (App.Current.MainWindow is MainWindow mainWindow)
                    mainWindow.CaptionLogButton.Visibility = Visibility.Collapsed;
            };

            CollapseTranslatedCaption(Translator.Setting.MainWindow.CaptionLogEnabled);
            ApplyFontSizes();
        }

        private async void TextBlock_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                await CopyTextAsync(textBlock.Text);
            }
        }

        private async void LogTranslation_MouseLeftButtonDown(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                string cleanText = RegexPatterns.NoticePrefix().Replace(textBlock.Text ?? string.Empty, string.Empty).Trim();
                await CopyTextAsync(cleanText);
            }
        }

        private async void EngineTranslationRow_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement element)
                return;

            string textToCopy = string.Empty;
            if (element.DataContext is EngineTranslationItem item && !string.IsNullOrWhiteSpace(item.PureTranslation))
            {
                textToCopy = item.PureTranslation;
            }
            else if (element.Tag is string tagText && !string.IsNullOrWhiteSpace(tagText))
            {
                textToCopy = tagText;
            }

            if (!string.IsNullOrWhiteSpace(textToCopy))
            {
                await CopyTextAsync(textToCopy);
            }
        }

        private static async Task CopyTextAsync(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                Clipboard.SetText(text);
                SnackbarHost.Show("Copied.", text, SnackbarType.Info, 100);
            }
            catch
            {
                SnackbarHost.Show("Copy Failed.", string.Empty, SnackbarType.Error, 100);
            }
            await Task.Delay(500);
        }

        private void ApplyFontSizes()
        {
            OriginalCaption.FontSize = Translator.Setting.MainWindow.OriginalFontSize;
            TranslatedFontSize = Translator.Setting.MainWindow.TranslatedFontSize;
        }

        private void OriginalCard_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;
            Translator.Setting.MainWindow.OriginalFontSize =
                AdjustFontSize(Translator.Setting.MainWindow.OriginalFontSize, e.Delta);
            ApplyFontSizes();
            e.Handled = true;
        }

        private void TranslatedCard_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.Control)
                return;
            Translator.Setting.MainWindow.TranslatedFontSize =
                AdjustFontSize(Translator.Setting.MainWindow.TranslatedFontSize, e.Delta);
            ApplyFontSizes();
            e.Handled = true;
        }

        private static int AdjustFontSize(int current, int wheelDelta)
        {
            int next = current + (wheelDelta > 0 ? StyleConsts.DELTA_FONT_SIZE : -StyleConsts.DELTA_FONT_SIZE);
            return Math.Clamp(next, StyleConsts.MIN_FONT_SIZE, StyleConsts.MAX_FONT_SIZE);
        }

        public void CollapseTranslatedCaption(bool isCollapsed)
        {
            var converter = new GridLengthConverter();

            if (isCollapsed)
            {
                TranslatedCaption_Row.Height = (GridLength)converter.ConvertFromString("Auto");
                LogCards.Visibility = Visibility.Visible;
            }
            else
            {
                TranslatedCaption_Row.Height = (GridLength)converter.ConvertFromString("*");
                LogCards.Visibility = Visibility.Collapsed;
            }
        }

        public void AutoHeight()
        {
            int activeCount = Math.Max(1, Translator.GetActiveApis().Count);
            int extraParallel = (activeCount - 1) * 34;
            if (App.Current.MainWindow is not MainWindow mainWindow)
                return;

            int height = Translator.Setting.MainWindow.CaptionLogEnabled
                ? CARD_HEIGHT * (Translator.Setting.DisplaySentences + 1) + extraParallel
                : (int)mainWindow.MinHeight + extraParallel;

            mainWindow.IsAutoHeight = true;
            mainWindow.AutoHeightAdjust(minHeight: height, maxHeight: height);
        }
    }
}
