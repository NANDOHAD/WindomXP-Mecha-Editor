using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.Localization;


    // UI文字列テーブルへの共通アクセス口。
public static class UILocalization
{
    public const string TableName = "UI";

    public static string Get(string entryKey, string japaneseFallback, params object[] arguments)
    {
        string template = japaneseFallback ?? string.Empty;

        if (!string.IsNullOrEmpty(entryKey))
        {
            try
            {
                string localized = new LocalizedString(TableName, entryKey).GetLocalizedString();
                if (!string.IsNullOrEmpty(localized))
                    template = localized;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[UILocalization] 翻訳の取得に失敗しました ({entryKey}): {exception.Message}");
            }
        }

        if (arguments == null || arguments.Length == 0)
            return template;

        try
        {
            return string.Format(CultureInfo.CurrentCulture, template, arguments);
        }
        catch (FormatException exception)
        {
            Debug.LogWarning($"[UILocalization] 翻訳文の書式が不正です ({entryKey}): {exception.Message}");
            return template;
        }
    }
}
