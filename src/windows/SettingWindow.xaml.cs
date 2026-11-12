using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using LiveCaptionsTranslator.apis;
using LiveCaptionsTranslator.models;

namespace LiveCaptionsTranslator
{
    public sealed class EngineOptionItem
    {
        public string ApiName { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    public partial class SettingWindow : Window
    {
        private static readonly List<EngineOptionItem> AllEngineDefinitions =
        [
            new() { ApiName = "OpenAI", DisplayName = "OpenAI (ChatGPT / 兼容接口)" },
            new() { ApiName = "DeepL", DisplayName = "DeepL (DeepL REST API)" },
            new() { ApiName = "IndexTranslate", DisplayName = "IndexTranslate (Bilibili 翻译模型 / 免配置)" },
            new() { ApiName = "Google2", DisplayName = "Google2 (Google Dictionary / 免配置)" },
            new() { ApiName = "Google", DisplayName = "Google (Google Translate / 免配置)" },
            new() { ApiName = "Ollama", DisplayName = "Ollama (本地大模型服务)" },
            new() { ApiName = "LMStudio", DisplayName = "LMStudio (本地 LM Studio 服务)" },
            new() { ApiName = "OpenRouter", DisplayName = "OpenRouter (多模型聚合网关)" },
            new() { ApiName = "Youdao", DisplayName = "Youdao (有道智云翻译)" },
            new() { ApiName = "Baidu", DisplayName = "Baidu (百度翻译开放平台)" },
            new() { ApiName = "MTranServer", DisplayName = "MTranServer (自建离线翻译服务)" },
            new() { ApiName = "LibreTranslate", DisplayName = "LibreTranslate (开源自建翻译)" }
        ];

        private static readonly Dictionary<string, string[]> DefaultModelSuggestions = new()
        {
            ["OpenAI"] = ["gpt-4o-mini", "gpt-4.1-mini", "gpt-4.1-nano", "deepseek-chat", "qwen-mt-turbo"],
            ["Ollama"] = ["qwen2.5:3b", "qwen2.5:7b", "llama3.2:3b", "gemma2:9b"],
            ["LMStudio"] = ["qwen2.5-7b-instruct", "llama-3.2-3b-instruct"],
            ["OpenRouter"] = ["openai/gpt-4o-mini", "google/gemini-2.0-flash-001", "deepseek/deepseek-chat"]
        };

        private bool isNewMode;
        private string currentApiName = "OpenAI";
        private bool isSyncingSecretBoxes;
        private bool isSecretVisible;
        private bool isUpdatingSelection;
        private string? initialSettingsSnapshotJson;

        public event Action<string, bool>? EngineSaved;

        public SettingWindow(string? targetApiName = null, bool isNewMode = false)
        {
            InitializeComponent();
            ApplicationThemeManager.ApplySystemTheme();
            CaptureInitialSnapshot();
            OpenForEngine(targetApiName, isNewMode);
        }

        public void OpenForEngine(string? targetApiName, bool newMode)
        {
            isNewMode = newMode;
            string initialApi = ResolveInitialApi(targetApiName, isNewMode);
            PopulateEngineOptions(initialApi);
        }

        private void CaptureInitialSnapshot()
        {
            if (Translator.Setting == null)
                return;

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    Converters = { new ConfigDictConverter() }
                };
                initialSettingsSnapshotJson = JsonSerializer.Serialize(Translator.Setting, options);
            }
            catch
            {
                initialSettingsSnapshotJson = null;
            }
        }

        private void RestoreInitialSnapshot()
        {
            if (string.IsNullOrEmpty(initialSettingsSnapshotJson) || Translator.Setting == null)
                return;

            try
            {
                var options = new JsonSerializerOptions
                {
                    Converters = { new ConfigDictConverter() }
                };
                var restored = JsonSerializer.Deserialize<Setting>(initialSettingsSnapshotJson, options);
                if (restored != null)
                {
                    Translator.Setting.Prompt = restored.Prompt;
                    Translator.Setting.Configs = restored.Configs;
                    Translator.Setting.ConfigIndices = restored.ConfigIndices;
                }
            }
            catch
            {
                // Ignore snapshot restore errors
            }
        }

        private static string ResolveInitialApi(string? targetApiName, bool newMode)
        {
            if (!string.IsNullOrWhiteSpace(targetApiName) &&
                TranslateAPI.TRANSLATE_FUNCTIONS.ContainsKey(targetApiName))
            {
                return targetApiName;
            }

            if (newMode && Translator.Setting?.ApiOrder != null)
            {
                var existing = Translator.Setting.ApiOrder.ToHashSet();
                var firstNotAdded = AllEngineDefinitions.FirstOrDefault(item => !existing.Contains(item.ApiName));
                if (firstNotAdded != null)
                    return firstNotAdded.ApiName;
            }

            return "OpenAI";
        }

        private void PopulateEngineOptions(string? preferredApiName = null)
        {
            var existingSet = Translator.Setting?.ApiOrder?.ToHashSet() ?? [];
            var options = isNewMode
                ? AllEngineDefinitions.Where(item => !existingSet.Contains(item.ApiName)).ToList()
                : AllEngineDefinitions.ToList();

            if (options.Count == 0)
                options = AllEngineDefinitions.ToList();

            isUpdatingSelection = true;
            EngineServiceComboBox.ItemsSource = options;

            string targetApi = preferredApiName ?? currentApiName;
            var selectedItem = options.FirstOrDefault(x => x.ApiName == targetApi) ?? options[0];
            EngineServiceComboBox.SelectedItem = selectedItem;
            isUpdatingSelection = false;

            SelectEngine(selectedItem.ApiName);
        }

        private void SelectEngine(string apiName)
        {
            currentApiName = apiName;
            DialogTitleText.Text = isNewMode
                ? $"New Translation Engine - {apiName}"
                : $"Configure Engine - {apiName}";

            HideTestResult();
            UpdateFieldVisibilityForEngine(apiName);
            LoadCurrentEngineConfigToForm();
        }

        private void UpdateFieldVisibilityForEngine(string apiName)
        {
            bool isFree = TranslateAPI.NO_CONFIG_APIS.Contains(apiName);
            bool isLlm = TranslateAPI.LLM_BASED_APIS.Contains(apiName) && !isFree;

            FreeEngineNoticeBorder.Visibility = isFree ? Visibility.Visible : Visibility.Collapsed;
            ProfilePanel.Visibility = isFree ? Visibility.Collapsed : Visibility.Visible;

            AppIdFieldPanel.Visibility = apiName is "Youdao" or "Baidu"
                ? Visibility.Visible
                : Visibility.Collapsed;
            AppIdLabelText.Text = apiName == "Youdao"
                ? "App Key (有道应用 ID)"
                : "App ID (百度应用 ID)";

            ApiKeyFieldPanel.Visibility = apiName is "OpenAI" or "DeepL" or "OpenRouter" or "Youdao" or "Baidu" or "MTranServer" or "LibreTranslate"
                ? Visibility.Visible
                : Visibility.Collapsed;
            ApiKeyLabelText.Text = apiName switch
            {
                "Youdao" => "App Secret (有道应用密钥)",
                "Baidu" => "App Secret (百度应用密钥)",
                "MTranServer" or "LibreTranslate" => "API Key (密钥，可选)",
                _ => "API Key (密钥)"
            };

            ApiUrlFieldPanel.Visibility = apiName is "OpenAI" or "DeepL" or "Ollama" or "LMStudio" or "Youdao" or "Baidu" or "MTranServer" or "LibreTranslate"
                ? Visibility.Visible
                : Visibility.Collapsed;
            ApiUrlLabelText.Text = apiName switch
            {
                "OpenAI" => "Base URL (可选自定义代理端点)",
                "Ollama" or "LMStudio" => "Server URL (本地服务端点)",
                _ => "API Endpoint (接口地址)"
            };
            ApiUrlHintText.Text = apiName switch
            {
                "OpenAI" => "请填写完整 Chat Completions 路径（通常以 /v1/chat/completions 结尾）。",
                "Ollama" => "仅填写基础地址（如 http://localhost:11434），程序会自动拼接 /api/chat。",
                "LMStudio" => "填写以 /api/v1 结尾的基础地址（如 http://localhost:1234/api/v1）。",
                _ => string.Empty
            };
            ApiUrlHintText.Visibility = string.IsNullOrEmpty(ApiUrlHintText.Text)
                ? Visibility.Collapsed
                : Visibility.Visible;

            ModelFieldPanel.Visibility = isLlm ? Visibility.Visible : Visibility.Collapsed;
            LoadModelsButton.Visibility = apiName == "LMStudio" ? Visibility.Visible : Visibility.Collapsed;
            TemperatureFieldPanel.Visibility = isLlm ? Visibility.Visible : Visibility.Collapsed;
            PromptFieldPanel.Visibility = isLlm ? Visibility.Visible : Visibility.Collapsed;

            SourceLangFieldPanel.Visibility = apiName == "MTranServer"
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (DefaultModelSuggestions.TryGetValue(apiName, out var suggestions))
                ModelComboBox.ItemsSource = suggestions;
            else
                ModelComboBox.ItemsSource = null;
        }

        private void LoadCurrentEngineConfigToForm()
        {
            if (Translator.Setting == null)
                return;

            PromptTextBox.Text = Translator.Setting.Prompt ?? string.Empty;

            if (TranslateAPI.NO_CONFIG_APIS.Contains(currentApiName))
                return;

            UpdateProfileIndexBadge();
            var config = Translator.Setting[currentApiName];

            SetSecretValue(string.Empty);
            AppIdTextBox.Text = string.Empty;
            ApiUrlTextBox.Text = string.Empty;
            ModelComboBox.Text = string.Empty;
            TemperatureNumberBox.Value = 1.0;
            SourceLangTextBox.Text = "en";

            switch (config)
            {
                case OpenAIConfig openAi:
                    SetSecretValue(openAi.ApiKey);
                    ApiUrlTextBox.Text = openAi.ApiUrl;
                    ModelComboBox.Text = openAi.ModelName;
                    TemperatureNumberBox.Value = openAi.Temperature;
                    break;
                case DeepLConfig deepL:
                    SetSecretValue(deepL.ApiKey);
                    ApiUrlTextBox.Text = deepL.ApiUrl;
                    break;
                case OllamaConfig ollama:
                    ApiUrlTextBox.Text = ollama.ApiUrl;
                    ModelComboBox.Text = ollama.ModelName;
                    TemperatureNumberBox.Value = ollama.Temperature;
                    break;
                case LMStudioConfig lmStudio:
                    ApiUrlTextBox.Text = lmStudio.ApiUrl;
                    ModelComboBox.Text = lmStudio.ModelName;
                    TemperatureNumberBox.Value = lmStudio.Temperature;
                    break;
                case OpenRouterConfig openRouter:
                    SetSecretValue(openRouter.ApiKey);
                    ModelComboBox.Text = openRouter.ModelName;
                    TemperatureNumberBox.Value = openRouter.Temperature;
                    break;
                case YoudaoConfig youdao:
                    AppIdTextBox.Text = youdao.AppKey;
                    SetSecretValue(youdao.AppSecret);
                    ApiUrlTextBox.Text = youdao.ApiUrl;
                    break;
                case BaiduConfig baidu:
                    AppIdTextBox.Text = baidu.AppId;
                    SetSecretValue(baidu.AppSecret);
                    ApiUrlTextBox.Text = baidu.ApiUrl;
                    break;
                case MTranServerConfig mtran:
                    SetSecretValue(mtran.ApiKey);
                    ApiUrlTextBox.Text = mtran.ApiUrl;
                    SourceLangTextBox.Text = mtran.SourceLanguage;
                    break;
                case LibreTranslateConfig libre:
                    SetSecretValue(libre.ApiKey);
                    ApiUrlTextBox.Text = libre.ApiUrl;
                    break;
            }
        }

        private void CommitFormToCurrentEngineConfig()
        {
            if (Translator.Setting == null)
                return;

            if (PromptFieldPanel.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(PromptTextBox.Text))
                Translator.Setting.Prompt = PromptTextBox.Text.Trim();

            if (TranslateAPI.NO_CONFIG_APIS.Contains(currentApiName))
                return;

            var config = Translator.Setting[currentApiName];
            string secret = GetSecretValue().Trim();
            string apiUrl = ApiUrlTextBox.Text.Trim();
            string modelName = ModelComboBox.Text.Trim();
            double temperature = Math.Round(TemperatureNumberBox.Value ?? 1.0, 2);

            switch (config)
            {
                case OpenAIConfig openAi:
                    openAi.ApiKey = secret;
                    if (!string.IsNullOrEmpty(apiUrl))
                        openAi.ApiUrl = apiUrl;
                    if (!string.IsNullOrEmpty(modelName))
                        openAi.ModelName = modelName;
                    openAi.Temperature = temperature;
                    break;
                case DeepLConfig deepL:
                    deepL.ApiKey = secret;
                    if (!string.IsNullOrEmpty(apiUrl))
                        deepL.ApiUrl = apiUrl;
                    break;
                case OllamaConfig ollama:
                    if (!string.IsNullOrEmpty(apiUrl))
                        ollama.ApiUrl = apiUrl;
                    if (!string.IsNullOrEmpty(modelName))
                        ollama.ModelName = modelName;
                    ollama.Temperature = temperature;
                    break;
                case LMStudioConfig lmStudio:
                    if (!string.IsNullOrEmpty(apiUrl))
                        lmStudio.ApiUrl = apiUrl;
                    if (!string.IsNullOrEmpty(modelName))
                        lmStudio.ModelName = modelName;
                    lmStudio.Temperature = temperature;
                    break;
                case OpenRouterConfig openRouter:
                    openRouter.ApiKey = secret;
                    if (!string.IsNullOrEmpty(modelName))
                        openRouter.ModelName = modelName;
                    openRouter.Temperature = temperature;
                    break;
                case YoudaoConfig youdao:
                    youdao.AppKey = AppIdTextBox.Text.Trim();
                    youdao.AppSecret = secret;
                    if (!string.IsNullOrEmpty(apiUrl))
                        youdao.ApiUrl = apiUrl;
                    break;
                case BaiduConfig baidu:
                    baidu.AppId = AppIdTextBox.Text.Trim();
                    baidu.AppSecret = secret;
                    if (!string.IsNullOrEmpty(apiUrl))
                        baidu.ApiUrl = apiUrl;
                    break;
                case MTranServerConfig mtran:
                    mtran.ApiKey = secret;
                    if (!string.IsNullOrEmpty(apiUrl))
                        mtran.ApiUrl = apiUrl;
                    if (!string.IsNullOrWhiteSpace(SourceLangTextBox.Text))
                        mtran.SourceLanguage = SourceLangTextBox.Text.Trim();
                    break;
                case LibreTranslateConfig libre:
                    libre.ApiKey = secret;
                    if (!string.IsNullOrEmpty(apiUrl))
                        libre.ApiUrl = apiUrl;
                    break;
            }
        }

        private void UpdateProfileIndexBadge()
        {
            if (Translator.Setting == null ||
                !Translator.Setting.Configs.TryGetValue(currentApiName, out var configs) ||
                !Translator.Setting.ConfigIndices.TryGetValue(currentApiName, out int index))
            {
                ProfileIndexText.Text = "1/1";
                return;
            }

            ProfileIndexText.Text = $"{index + 1}/{Math.Max(1, configs.Count)}";
        }

        private void SetSecretValue(string? value)
        {
            isSyncingSecretBoxes = true;
            string safeValue = value ?? string.Empty;
            ApiKeyPasswordBox.Password = safeValue;
            ApiKeyTextBox.Text = safeValue;
            isSyncingSecretBoxes = false;
        }

        private string GetSecretValue()
        {
            return isSecretVisible ? ApiKeyTextBox.Text : ApiKeyPasswordBox.Password;
        }

        private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (isSyncingSecretBoxes)
                return;

            isSyncingSecretBoxes = true;
            ApiKeyTextBox.Text = ApiKeyPasswordBox.Password;
            isSyncingSecretBoxes = false;
        }

        private void ApiKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isSyncingSecretBoxes)
                return;

            isSyncingSecretBoxes = true;
            ApiKeyPasswordBox.Password = ApiKeyTextBox.Text;
            isSyncingSecretBoxes = false;
        }

        private void ToggleKeyVisibilityButton_Click(object sender, RoutedEventArgs e)
        {
            isSecretVisible = !isSecretVisible;
            if (isSecretVisible)
            {
                ApiKeyTextBox.Text = ApiKeyPasswordBox.Password;
                ApiKeyPasswordBox.Visibility = Visibility.Collapsed;
                ApiKeyTextBox.Visibility = Visibility.Visible;
                ToggleKeyVisibilityIcon.Symbol = SymbolRegular.EyeOff16;
            }
            else
            {
                ApiKeyPasswordBox.Password = ApiKeyTextBox.Text;
                ApiKeyTextBox.Visibility = Visibility.Collapsed;
                ApiKeyPasswordBox.Visibility = Visibility.Visible;
                ToggleKeyVisibilityIcon.Symbol = SymbolRegular.Eye16;
            }
        }

        private void EngineServiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingSelection)
                return;

            if (EngineServiceComboBox.SelectedItem is EngineOptionItem option)
            {
                CommitFormToCurrentEngineConfig();
                SelectEngine(option.ApiName);
            }
        }

        private void PriorProfile_Click(object sender, RoutedEventArgs e)
        {
            SwitchProfile(-1);
        }

        private void NextProfile_Click(object sender, RoutedEventArgs e)
        {
            SwitchProfile(1);
        }

        private void SwitchProfile(int delta)
        {
            if (Translator.Setting == null || TranslateAPI.NO_CONFIG_APIS.Contains(currentApiName))
                return;

            CommitFormToCurrentEngineConfig();
            var configs = Translator.Setting.Configs[currentApiName];
            int nextIndex = Translator.Setting.ConfigIndices[currentApiName] + delta;
            if (nextIndex < 0 || nextIndex >= configs.Count)
                return;

            Translator.Setting.ConfigIndices[currentApiName] = nextIndex;
            LoadCurrentEngineConfigToForm();
        }

        private void NewProfile_Click(object sender, RoutedEventArgs e)
        {
            if (Translator.Setting == null || TranslateAPI.NO_CONFIG_APIS.Contains(currentApiName))
                return;

            CommitFormToCurrentEngineConfig();
            var configs = Translator.Setting.Configs[currentApiName];
            int currentIndex = Translator.Setting.ConfigIndices[currentApiName];

            var type = Type.GetType($"LiveCaptionsTranslator.models.{currentApiName}Config");
            if (type == null || Activator.CreateInstance(type) is not TranslateAPIConfig newConfig)
                return;

            configs.Insert(currentIndex + 1, newConfig);
            Translator.Setting.ConfigIndices[currentApiName] = currentIndex + 1;
            LoadCurrentEngineConfigToForm();
        }

        private void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (Translator.Setting == null || TranslateAPI.NO_CONFIG_APIS.Contains(currentApiName))
                return;

            var configs = Translator.Setting.Configs[currentApiName];
            int currentIndex = Translator.Setting.ConfigIndices[currentApiName];
            if (configs.Count <= 1)
            {
                ShowTestResult("至少需要保留 1 个配置档位 (Profile 1/1)。", isError: true);
                return;
            }

            configs.RemoveAt(currentIndex);
            Translator.Setting.ConfigIndices[currentApiName] = Math.Max(0, Math.Min(configs.Count - 1, currentIndex));
            LoadCurrentEngineConfigToForm();
        }

        private async void LoadModelsButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentApiName != "LMStudio")
                return;

            string baseUrl = ApiUrlTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                ShowTestResult("请先填写 LMStudio Server URL。", isError: true);
                return;
            }

            LoadModelsButton.IsEnabled = false;
            try
            {
                var models = await ModelsApiService.FetchModelsAsync("LMStudio", baseUrl);
                if (models.Count > 0)
                {
                    var modelIds = models.Select(m => m.Id).ToArray();
                    ModelComboBox.ItemsSource = modelIds;
                    ModelComboBox.Text = modelIds[0];
                    ShowTestResult($"已从 LMStudio 加载 {models.Count} 个模型。", isError: false);
                }
                else
                {
                    ShowTestResult("未检测到可用模型，请确认 LMStudio Local Server 已启动。", isError: true);
                }
            }
            catch (Exception ex)
            {
                ShowTestResult($"加载模型失败：{ex.Message}", isError: true);
            }
            finally
            {
                LoadModelsButton.IsEnabled = true;
            }
        }

        private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TranslateAPI.TRANSLATE_FUNCTIONS.TryGetValue(currentApiName, out var translateFunc))
                return;

            CommitFormToCurrentEngineConfig();
            TestConnectionButton.IsEnabled = false;
            ShowTestResult($"正在测试 {currentApiName} 连接...", isError: false);

            var sw = Stopwatch.StartNew();
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                string result = await Task.Run(() => translateFunc("Hello, world!", cts.Token));
                sw.Stop();

                bool failed = string.IsNullOrWhiteSpace(result) ||
                              result.Contains("[ERROR]", StringComparison.OrdinalIgnoreCase) ||
                              result.Contains("[WARNING]", StringComparison.OrdinalIgnoreCase);

                if (failed)
                    ShowTestResult($"测试失败 ({sw.ElapsedMilliseconds} ms)：{result}", isError: true);
                else
                    ShowTestResult($"连接成功 ({sw.ElapsedMilliseconds} ms) — 示例译文：{result}", isError: false);
            }
            catch (Exception ex)
            {
                sw.Stop();
                ShowTestResult($"测试异常 ({sw.ElapsedMilliseconds} ms)：{ex.Message}", isError: true);
            }
            finally
            {
                TestConnectionButton.IsEnabled = true;
            }
        }

        private void ShowTestResult(string message, bool isError)
        {
            TestResultBorder.Visibility = Visibility.Visible;
            TestResultBorder.BorderBrush = new SolidColorBrush(
                isError ? System.Windows.Media.Color.FromRgb(0xF8, 0x71, 0x71) : System.Windows.Media.Color.FromRgb(0x38, 0xBD, 0xF8));
            TestResultText.Foreground = new SolidColorBrush(
                isError ? System.Windows.Media.Color.FromRgb(0xFC, 0xA5, 0xA5) : System.Windows.Media.Color.FromRgb(0xE2, 0xE8, 0xF0));
            TestResultText.Text = message;
        }

        private void HideTestResult()
        {
            TestResultBorder.Visibility = Visibility.Collapsed;
            TestResultText.Text = string.Empty;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            CommitFormToCurrentEngineConfig();
            Translator.Setting?.OnPropertyChanged("Configs");
            Translator.Setting?.Save();

            EngineSaved?.Invoke(currentApiName, isNewMode);
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            RestoreInitialSnapshot();
            Close();
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
