using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Dropdown))]
public sealed class UILanguageSelector : MonoBehaviour
{
    const string LocalePreferenceKey = "WindomXPMechaEditor.Locale";

    static readonly string[] LocaleCodes = { "ja", "en" };
    static readonly string[] LocaleNames = { "日本語", "English" };

    Dropdown languageDropdown;

    IEnumerator Start()
    {
        languageDropdown = GetComponent<Dropdown>();
        yield return LocalizationSettings.InitializationOperation;

        languageDropdown.ClearOptions();
        languageDropdown.AddOptions(new List<string>(LocaleNames));

        string code = PlayerPrefs.GetString(LocalePreferenceKey, string.Empty);
        if (string.IsNullOrEmpty(code))
            code = Application.systemLanguage == SystemLanguage.English ? "en" : "ja";

        int selectedIndex = code == "en" ? 1 : 0;
        languageDropdown.SetValueWithoutNotify(selectedIndex);
        ApplyLocale(selectedIndex, false);
        languageDropdown.onValueChanged.AddListener(OnLanguageChanged);
    }

    void OnDestroy()
    {
        if (languageDropdown != null)
            languageDropdown.onValueChanged.RemoveListener(OnLanguageChanged);
    }

    void OnLanguageChanged(int index)
    {
        ApplyLocale(index, true);
    }

    void ApplyLocale(int index, bool savePreference)
    {
        if (index < 0 || index >= LocaleCodes.Length)
            index = 0;

        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(LocaleCodes[index]);
        if (locale == null)
        {
            Debug.LogWarning($"[UILanguageSelector] Localeが見つかりません: {LocaleCodes[index]}");
            return;
        }

        LocalizationSettings.SelectedLocale = locale;
        if (savePreference)
        {
            PlayerPrefs.SetString(LocalePreferenceKey, LocaleCodes[index]);
            PlayerPrefs.Save();
        }
    }
}
