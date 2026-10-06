using System.Collections.Generic;
using _Core.Localization;
using _Core.Settings;
using _Core.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace _Project.UI
{
    /// <summary>
    /// One button per shipped language (the Locales in the Localization settings), current one highlighted.
    /// A choice applies immediately and is saved. Button labels are the Locale names, so name each Locale
    /// in its own language ("Türkçe", "Español").
    /// </summary>
    public class LanguagePopup : AnimatedUIPopup
    {
        [SerializeField] private Button languageButtonTemplate;
        [SerializeField] private Button closeButton;
        // Multiplied with the button's own tint: the selected language keeps full brightness, the others are dimmed.
        [SerializeField] private Color normalColor = new Color(0.55f, 0.6f, 0.7f, 1f);
        [SerializeField] private Color selectedColor = Color.white;

        private readonly List<(Button button, string code)> _buttons = new List<(Button, string)>();

        protected override void Awake()
        {
            base.Awake();
            languageButtonTemplate.gameObject.SetActive(false);
            closeButton.onClick.AddListener(OnCloseClicked);
        }

        private void OnDestroy()
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            foreach ((Button button, string _) in _buttons)
                button.onClick.RemoveAllListeners();
        }

        protected override void OnShown()
        {
            BuildButtons();
            RefreshHighlight();
        }

        // Built once: the locale list does not change at runtime.
        private void BuildButtons()
        {
            if (_buttons.Count > 0 || LocalizationManager.Instance == null)
                return;

            foreach (Locale locale in LocalizationManager.Instance.AvailableLocales)
            {
                if (locale == null)
                    continue;

                string code = locale.Identifier.Code;
                Button button = Instantiate(languageButtonTemplate, languageButtonTemplate.transform.parent);
                button.name = $"LanguageButton_{code}";
                button.gameObject.SetActive(true);

                // Keep the buttons in locale order right after the template (above the Close button).
                button.transform.SetSiblingIndex(languageButtonTemplate.transform.GetSiblingIndex() + 1 + _buttons.Count);

                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.text = locale.LocaleName;

                button.onClick.AddListener(() => OnLanguageClicked(code));
                _buttons.Add((button, code));
            }
        }

        private void RefreshHighlight()
        {
            string current = LocalizationManager.Instance != null ? LocalizationManager.Instance.CurrentLanguage : string.Empty;

            foreach ((Button button, string code) in _buttons)
            {
                if (button.targetGraphic != null)
                    button.targetGraphic.color = code == current ? selectedColor : normalColor;
            }
        }

        private void OnLanguageClicked(string code)
        {
            SettingsManager.Instance.SetLanguage(code);
            SettingsManager.Instance.Commit();

            // The selected locale switches synchronously; texts refresh as the new tables load.
            RefreshHighlight();
        }

        private void OnCloseClicked()
        {
            Close();
        }
    }
}
