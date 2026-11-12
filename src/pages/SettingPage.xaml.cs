using System.Collections.ObjectModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using LiveCaptionsTranslator.apis;
using LiveCaptionsTranslator.models;
using LiveCaptionsTranslator.utils;
using LiveCaptionsTranslator.Utils;

namespace LiveCaptionsTranslator
{
    public class TranslationEngineItem
    {
        private static readonly Brush EnabledRowBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x28, 0xFF, 0xFF, 0xFF));
        private static readonly Brush DisabledRowBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(0x12, 0xFF, 0xFF, 0xFF));
        private static readonly Brush ActiveTrashBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF8, 0x71, 0x71));
        private static readonly Brush MutedTrashBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x88, 0x88, 0x88));

        public string ApiName { get; init; } = string.Empty;
        public bool IsEnabled { get; init; }
        public int ActiveRank { get; init; }
        public bool CanMoveUp { get; init; }
        public bool CanMoveDown { get; init; }
        public bool CanDelete { get; init; }
        public bool IsFreeApi { get; init; }

        public string OrderBadge => IsEnabled ? $"{ActiveRank}." : string.Empty;
        public FontWeight TitleFontWeight => IsEnabled ? FontWeights.SemiBold : FontWeights.Regular;
        public double NameOpacity => IsEnabled ? 1.0 : 0.8;
        public Brush RowBackground => IsEnabled ? EnabledRowBrush : DisabledRowBrush;
        public Brush TrashIconBrush => IsFreeApi ? MutedTrashBrush : ActiveTrashBrush;
        public Visibility FreeTagVisibility => IsFreeApi ? Visibility.Visible : Visibility.Collapsed;
        public Visibility ConfigButtonVisibility => IsFreeApi ? Visibility.Collapsed : Visibility.Visible;
    }

    public partial class SettingPage : Page
    {
        public const int MIN_HEIGHT = 345;

        private static SettingWindow? SettingWindow;
        private readonly ObservableCollection<TranslationEngineItem> engineItems = [];

        public SettingPage()
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            DataContext = Translator.Setting;
            EngineItemsControl.ItemsSource = engineItems;

            Loaded += (s, e) =>
            {
                (App.Current.MainWindow as MainWindow)?.AutoHeightAdjust(minHeight: MIN_HEIGHT, maxHeight: MIN_HEIGHT);
                RefreshEngineItems();
                CheckForFirstUse();
            };

            RefreshEngineItems();
            LoadAPISetting();
        }

        private void LiveCaptionsButton_click(object sender, RoutedEventArgs e)
        {
            if (Translator.Window == null)
                return;

            bool isHide = Translator.Window.Current.BoundingRectangle == Rect.Empty;
            if (isHide)
            {
                LiveCaptionsHandler.RestoreLiveCaptions(Translator.Window);
                ButtonText.Text = "Hide";
            }
            else
            {
                LiveCaptionsHandler.HideLiveCaptions(Translator.Window);
                ButtonText.Text = "Show";
            }
        }

        private void EngineCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox checkBox || checkBox.Tag is not string apiName || Translator.Setting == null)
                return;

            var orderedApis = GetCurrentOrderedApis();
            var enabledApis = Translator.GetActiveApis();

            if (checkBox.IsChecked == true)
            {
                if (!enabledApis.Contains(apiName))
                {
                    enabledApis.Add(apiName);
                    orderedApis.Remove(apiName);
                    int insertIndex = Math.Min(enabledApis.Count - 1, orderedApis.Count);
                    orderedApis.Insert(insertIndex, apiName);
                }
            }
            else
            {
                if (enabledApis.Count <= 1 && enabledApis.Contains(apiName))
                {
                    checkBox.IsChecked = true;
                    SnackbarHost.Show("至少保留一个翻译引擎", "请至少保持 1 个翻译引擎处于启用状态。", SnackbarType.Warning, timeout: 2);
                    return;
                }

                enabledApis.Remove(apiName);
                orderedApis.Remove(apiName);
                int insertIndex = Math.Min(enabledApis.Count, orderedApis.Count);
                orderedApis.Insert(insertIndex, apiName);
            }

            ApplyEngineSelection(orderedApis, enabledApis);
        }

        private void MoveEngineUp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.Tag is not string apiName || Translator.Setting == null)
                return;

            var orderedApis = GetCurrentOrderedApis();
            var enabledApis = Translator.GetActiveApis();
            int index = orderedApis.IndexOf(apiName);
            if (index <= 0)
                return;

            (orderedApis[index - 1], orderedApis[index]) = (orderedApis[index], orderedApis[index - 1]);
            if (!enabledApis.Contains(apiName) && index - 1 < enabledApis.Count)
                enabledApis.Add(apiName);

            ApplyEngineSelection(orderedApis, enabledApis);
        }

        private void MoveEngineDown_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.Tag is not string apiName || Translator.Setting == null)
                return;

            var orderedApis = GetCurrentOrderedApis();
            var enabledApis = Translator.GetActiveApis();
            int index = orderedApis.IndexOf(apiName);
            if (index < 0 || index >= orderedApis.Count - 1)
                return;

            if (enabledApis.Contains(apiName) && index == enabledApis.Count - 1 && enabledApis.Count > 1)
                return;

            (orderedApis[index], orderedApis[index + 1]) = (orderedApis[index + 1], orderedApis[index]);
            ApplyEngineSelection(orderedApis, enabledApis);
        }

        private void NewEngineButton_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingWindow();
        }

        private void DeleteEngine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.Tag is not string apiName || Translator.Setting == null)
                return;

            var orderedApis = GetCurrentOrderedApis();
            var enabledApis = Translator.GetActiveApis();

            if (orderedApis.Count <= 1 || (enabledApis.Count <= 1 && enabledApis.Contains(apiName)))
            {
                SnackbarHost.Show("至少保留一个翻译引擎", "请至少保持 1 个翻译引擎处于启用状态。", SnackbarType.Warning, timeout: 2);
                return;
            }

            orderedApis.Remove(apiName);
            enabledApis.Remove(apiName);
            ApplyEngineSelection(orderedApis, enabledApis);
        }

        private void EngineConfigButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement element || element.Tag is not string apiName)
                return;

            OpenSettingWindow(apiName);
        }

        private static List<string> GetCurrentOrderedApis()
        {
            Translator.Setting?.NormalizeApiOrder();
            return Translator.Setting?.ApiOrder?.ToList() ?? [];
        }

        private void ApplyEngineSelection(List<string> orderedApis, List<string> enabledApis)
        {
            Translator.Setting?.UpdateEngineSelection(orderedApis, enabledApis);
            RefreshEngineItems();
            LoadAPISetting();
            Translator.EnqueueActiveApisForCurrentCaption();
        }

        private void RefreshEngineItems()
        {
            var orderedApis = GetCurrentOrderedApis();
            var enabledApis = Translator.GetActiveApis();
            var enabledSet = enabledApis.ToHashSet();

            engineItems.Clear();
            int activeRank = 0;
            for (int i = 0; i < orderedApis.Count; i++)
            {
                string api = orderedApis[i];
                bool isEnabled = enabledSet.Contains(api);
                if (isEnabled)
                    activeRank++;

                bool canMoveUp = isEnabled ? activeRank > 1 : i > enabledSet.Count;
                bool canMoveDown = isEnabled ? activeRank < enabledSet.Count : i < orderedApis.Count - 1;
                bool canDelete = orderedApis.Count > 1 && (!isEnabled || enabledSet.Count > 1);

                engineItems.Add(new TranslationEngineItem
                {
                    ApiName = api,
                    IsEnabled = isEnabled,
                    ActiveRank = isEnabled ? activeRank : 0,
                    CanMoveUp = canMoveUp,
                    CanMoveDown = canMoveDown,
                    CanDelete = canDelete,
                    IsFreeApi = TranslateAPI.NO_CONFIG_APIS.Contains(api)
                });
            }
        }

        private void TargetLangBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TargetLangBox.SelectedItem != null)
                Translator.Setting.TargetLanguage = TargetLangBox.SelectedItem.ToString();
        }

        private void TargetLangBox_LostFocus(object sender, RoutedEventArgs e)
        {
            Translator.Setting.TargetLanguage = TargetLangBox.Text;
        }

        private void APISettingButton_click(object sender, RoutedEventArgs e)
        {
            OpenSettingWindow();
        }

        private static void OpenSettingWindow(string? targetSection = null)
        {
            if (SettingWindow != null && SettingWindow.IsLoaded)
            {
                SettingWindow.Activate();
                NavigateSettingWindowSection(targetSection);
            }
            else
            {
                SettingWindow = new SettingWindow();
                SettingWindow.Closed += (sender, args) => SettingWindow = null;
                SettingWindow.Loaded += (sender, args) => NavigateSettingWindowSection(targetSection);
                SettingWindow.Show();
            }
        }

        private static void NavigateSettingWindowSection(string? targetSection)
        {
            if (SettingWindow == null || string.IsNullOrEmpty(targetSection))
                return;

            if (SettingWindow.FindName($"{targetSection}Button") is System.Windows.Controls.Button navButton)
                navButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        private void Contexts_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.DisplaySentences = Translator.Setting.NumContexts;
        }

        private void DisplaySentences_ValueChanged(object sender, NumberBoxValueChangedEventArgs args)
        {
            if (Translator.Setting.DisplaySentences > Translator.Setting.NumContexts)
                Translator.Setting.NumContexts = Translator.Setting.DisplaySentences;
            Translator.Caption.OnPropertyChanged("DisplayLogCards");
            Translator.Caption.OnPropertyChanged("OverlayPreviousTranslation");
        }

        private void LiveCaptionsInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Show();
        }

        private void LiveCaptionsInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            LiveCaptionsInfoFlyout.Hide();
        }

        private void FrequencyInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            FrequencyInfoFlyout.Show();
        }

        private void FrequencyInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            FrequencyInfoFlyout.Hide();
        }

        private void TranslateAPIInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            TranslateAPIInfoFlyout.Show();
        }

        private void TranslateAPIInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            TranslateAPIInfoFlyout.Hide();
        }

        private void TargetLangInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            TargetLangInfoFlyout.Show();
        }

        private void TargetLangInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            TargetLangInfoFlyout.Hide();
        }

        private void CaptionLogMaxInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            CaptionLogMaxInfoFlyout.Show();
        }

        private void CaptionLogMaxInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            CaptionLogMaxInfoFlyout.Hide();
        }

        private void ContextAwareInfo_MouseEnter(object sender, MouseEventArgs e)
        {
            ContextAwareInfoFlyout.Show();
        }

        private void ContextAwareInfo_MouseLeave(object sender, MouseEventArgs e)
        {
            ContextAwareInfoFlyout.Hide();
        }

        private void CheckForFirstUse()
        {
            if (Translator.FirstUseFlag)
                ButtonText.Text = "Hide";
        }

        public void LoadAPISetting()
        {
            var configType = Translator.Setting[Translator.Setting.ApiName].GetType();
            var languagesProp = configType.GetProperty(
                "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);

            // Traverse base classes to find `SupportedLanguages`
            while (configType != null && languagesProp == null)
            {
                configType = configType.BaseType;
                languagesProp = configType.GetProperty(
                    "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);
            }
            if (languagesProp == null)
                languagesProp = typeof(TranslateAPIConfig).GetProperty(
                    "SupportedLanguages", BindingFlags.Public | BindingFlags.Static);

            var supportedLanguages = (Dictionary<string, string>)languagesProp.GetValue(null);
            TargetLangBox.ItemsSource = supportedLanguages.Keys;

            string targetLang = Translator.Setting.TargetLanguage;
            if (!supportedLanguages.ContainsKey(targetLang))
                supportedLanguages[targetLang] = targetLang;
            TargetLangBox.SelectedItem = targetLang;
        }
    }
}
