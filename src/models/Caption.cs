using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;

using LiveCaptionsTranslator.utils;

namespace LiveCaptionsTranslator.models
{
    public class EngineTranslationItem
    {
        public string ApiName { get; init; } = string.Empty;
        public bool ShowEngineTag { get; init; }
        public string NoticePrefix { get; init; } = string.Empty;
        public string PureTranslation { get; init; } = string.Empty;
        public string MainShortenedTranslation { get; init; } = string.Empty;

        public string EngineBadgeText => ShowEngineTag && !string.IsNullOrEmpty(ApiName) ? ApiName : string.Empty;
        public Visibility EngineBadgeVisibility => ShowEngineTag && !string.IsNullOrEmpty(ApiName)
            ? Visibility.Visible
            : Visibility.Collapsed;

        public string MainDisplayText
        {
            get
            {
                string body = string.IsNullOrEmpty(MainShortenedTranslation) ? PureTranslation : MainShortenedTranslation;
                return string.IsNullOrEmpty(NoticePrefix) ? body : $"{NoticePrefix} {body}".Trim();
            }
        }

        public string OverlayDisplayText
        {
            get
            {
                var parts = new List<string>(3);
                if (ShowEngineTag && !string.IsNullOrEmpty(ApiName))
                    parts.Add($"[{ApiName}]");
                if (!string.IsNullOrEmpty(NoticePrefix))
                    parts.Add(NoticePrefix);
                if (!string.IsNullOrEmpty(PureTranslation))
                    parts.Add(PureTranslation);
                return parts.Count > 0 ? string.Join(" ", parts) : " ";
            }
        }
    }

    public class Caption : INotifyPropertyChanged
    {
        public const int MAX_CONTEXTS = 10;

        private static Caption? instance = null;
        public event PropertyChangedEventHandler? PropertyChanged;

        private string displayOriginalCaption = string.Empty;
        private string displayTranslatedCaption = string.Empty;
        private string overlayOriginalCaption = " ";
        private string overlayCurrentTranslation = " ";
        private string overlayNoticePrefix = " ";
        private IReadOnlyList<EngineTranslationItem> engineTranslations =
        [
            new EngineTranslationItem { PureTranslation = " " }
        ];

        public string OriginalCaption { get; set; } = string.Empty;
        public string TranslatedCaption { get; set; } = string.Empty;

        public Queue<TranslationHistoryEntry> Contexts { get; } = new(MAX_CONTEXTS);

        public IEnumerable<TranslationHistoryEntry> AwareContexts => GetPreviousContexts(Translator.Setting.NumContexts);
        public string AwareContextsCaption => GetPreviousText(Translator.Setting.NumContexts, TextType.Caption);

        public IEnumerable<TranslationHistoryEntry> DisplayLogCards =>
            GetPreviousContexts(Translator.Setting.DisplaySentences).Reverse();

        public string DisplayOriginalCaption
        {
            get => displayOriginalCaption;
            set
            {
                displayOriginalCaption = value;
                OnPropertyChanged("DisplayOriginalCaption");
            }
        }
        public string DisplayTranslatedCaption
        {
            get => displayTranslatedCaption;
            set
            {
                displayTranslatedCaption = value;
                OnPropertyChanged("DisplayTranslatedCaption");
            }
        }

        public string OverlayOriginalCaption
        {
            get => overlayOriginalCaption;
            set
            {
                overlayOriginalCaption = value;
                OnPropertyChanged("OverlayOriginalCaption");
            }
        }
        public string OverlayNoticePrefix
        {
            get => overlayNoticePrefix;
            set
            {
                overlayNoticePrefix = value;
                OnPropertyChanged("OverlayNoticePrefix");
            }
        }
        public string OverlayCurrentTranslation
        {
            get => overlayCurrentTranslation;
            set
            {
                overlayCurrentTranslation = value;
                OnPropertyChanged("OverlayCurrentTranslation");
            }
        }

        public IReadOnlyList<EngineTranslationItem> EngineTranslations
        {
            get => engineTranslations;
            set
            {
                engineTranslations = value ?? [];
                OnPropertyChanged("EngineTranslations");
            }
        }

        public string OverlayPreviousTranslation =>
            GetPreviousText(Translator.Setting.DisplaySentences, TextType.Translation).Trim();

        public Visibility OverlayPreviousTranslationVisibility =>
            Translator.Setting.DisplaySentences > 0 && !string.IsNullOrWhiteSpace(OverlayPreviousTranslation)
                ? Visibility.Visible
                : Visibility.Collapsed;

        private Caption()
        {
        }

        public static Caption GetInstance()
        {
            if (instance != null)
                return instance;
            instance = new Caption();
            return instance;
        }

        public string GetPreviousText(int count, TextType textType)
        {
            TranslationHistoryEntry[] snapshot;
            lock (Contexts)
            {
                if (count <= 0 || Contexts.Count == 0)
                    return string.Empty;
                snapshot = Contexts.ToArray();
            }

            var prev = snapshot
                .Reverse().Take(count).Reverse()
                .Select(entry => entry == null || string.CompareOrdinal(entry.TranslatedText, "N/A") == 0 ||
                                 entry.TranslatedText.Contains("[ERROR]") || entry.TranslatedText.Contains("[WARNING]") ?
                    "" : (textType == TextType.Caption ? entry.SourceText : entry.TranslatedText))
                .Aggregate((accu, cur) =>
                {
                    if (!string.IsNullOrEmpty(accu))
                    {
                        if (Array.IndexOf(TextUtil.PUNC_EOS, accu[^1]) == -1)
                            accu += TextUtil.isCJChar(accu[^1]) ? "。" : ". ";
                        else
                            accu += TextUtil.isCJChar(accu[^1]) ? "" : " ";
                    }
                    cur = RegexPatterns.NoticePrefix().Replace(cur, "");
                    return accu + cur;
                });

            if (textType == TextType.Translation)
                prev = RegexPatterns.NoticePrefix().Replace(prev, "");
            if (!string.IsNullOrEmpty(prev) && Array.IndexOf(TextUtil.PUNC_EOS, prev[^1]) == -1)
                prev += TextUtil.isCJChar(prev[^1]) ? "。" : ".";
            if (!string.IsNullOrEmpty(prev) && Encoding.UTF8.GetByteCount(prev[^1].ToString()) < 2)
                prev += " ";
            return prev;
        }

        public IEnumerable<TranslationHistoryEntry> GetPreviousContexts(int count)
        {
            TranslationHistoryEntry[] snapshot;
            lock (Contexts)
            {
                if (count <= 0 || Contexts.Count == 0)
                    return [];
                snapshot = Contexts.ToArray();
            }

            return snapshot
                .Reverse().Take(count).Reverse()
                .Where(entry => entry != null && string.CompareOrdinal(entry.TranslatedText, "N/A") != 0 &&
                                !entry.TranslatedText.Contains("[ERROR]") &&
                                !entry.TranslatedText.Contains("[WARNING]"));
        }

        public void OnPropertyChanged([CallerMemberName] string propName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
            if (propName == nameof(OverlayPreviousTranslation))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OverlayPreviousTranslationVisibility)));
        }
    }

    public enum TextType
    {
        Caption,
        Translation
    }
}
