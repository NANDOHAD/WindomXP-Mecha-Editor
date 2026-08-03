using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LocalizationProjectSetup
{
    const string ScenePath = "Assets/UI_MechaClean.unity";
    const string LocalizationRoot = "Assets/Localization";
    const string LocaleRoot = LocalizationRoot + "/Locales";
    const string TableRoot = LocalizationRoot + "/Tables";

    sealed class Translation
    {
        public readonly string Key;
        public readonly string Japanese;
        public readonly string English;

        public Translation(string key, string japanese, string english)
        {
            Key = key;
            Japanese = japanese;
            English = english;
        }

        public bool Matches(string value)
        {
            string normalized = Normalize(value);
            return normalized == Normalize(Japanese) || normalized == Normalize(English);
        }
    }

    static Translation Fixed(string japanese, string english)
    {
        string hash = Hash128.Compute(japanese).ToString().Substring(0, 16);
        return new Translation("fixed." + hash, japanese, english);
    }

    static Translation Dynamic(string key, string japanese, string english)
    {
        return new Translation(key, japanese, english);
    }

    static readonly Translation[] FixedTranslations =
    {
        Fixed("3Dハンドルツール", "3D Handle Tool"),
        Fixed("閉じる", "Close"),
        Fixed("制約データ", "Constraint Data"),
        Fixed("実行できない操作です。", "This operation is not available."),
        Fixed("HODの名前を変更する", "Rename HOD"),
        Fixed("HODを書き出す", "Export HOD"),
        Fixed("HODを読み込む", "Load HOD"),
        Fixed("HODを貼り付ける", "Paste HOD"),
        Fixed("HODファイルを書き出す", "Export HOD File"),
        Fixed("HODファイル選択", "Select HOD File"),
        Fixed("HOD編集", "HOD Editor"),
        Fixed("下限", "Lower"),
        Fixed("中立", "Neutral"),
        Fixed("OK", "OK"),
        Fixed("オブジェクトを上書き", "Object Override"),
        Fixed("プロシージャルヘルパー", "Procedural Helper"),
        Fixed("ROBOフォルダーのディレクトリパス", "ROBO Folder Path"),
        Fixed("パーツを削除", "Remove Part"),
        Fixed("パーツ名を変更／交換", "Rename/Swap Part"),
        Fixed("Robo.hodをロードする", "Load Robo.hod"),
        Fixed("Script.aniをロードする", "Load Script.ani"),
        Fixed("Script.aniを上書き保存する", "Overwrite Script.ani"),
        Fixed("Script.sptの編集", "Edit Script.spt"),
        Fixed("Script.sptファイルの編集", "Edit Script.spt File"),
        Fixed("Scriptのフレーム", "Script Frame"),
        Fixed("Scriptの先頭部分", "Script Header"),
        Fixed("Script編集", "Script Editor"),
        Fixed("待機中...", "Standby..."),
        Fixed("上限", "Upper"),
        Fixed("hangar.hodをロードする", "Load hangar.hod"),
        Fixed("その他", "Other"),
        Fixed("アニメリストをダンプ", "Dump Animation List"),
        Fixed("アニメリストを同期する", "Synchronize Animation List"),
        Fixed("アニメ名の変更", "Rename Animation"),
        Fixed("アプリケーションバージョン:Ver.1.00", "Application Version: Ver.1.00"),
        Fixed("環境設定", "Configuration"),
        Fixed("カメラリセット", "Reset Camera"),
        Fixed("カメラ移動速度", "Camera Move Speed"),
        Fixed("カメラ設定", "Camera Settings"),
        Fixed("キャンセル", "Cancel"),
        Fixed("サブ1（104番）", "Sub 1 (No. 104)"),
        Fixed("サブ2（105番）", "Sub 2 (No. 105)"),
        Fixed("ステップ時の機体角度（0～360）", "Mecha Angle During Step (0-360)"),
        Fixed("ツールの種類", "Tool Type"),
        Fixed("テキストを入力してください", "Enter text"),
        Fixed("ディレクトリパスを入力してください...", "Enter a directory path..."),
        Fixed("パーツツリー", "Part Tree"),
        Fixed("パーツ編集", "Part Editor"),
        Fixed("パーツ選択", "Select Part"),
        Fixed("ファイル", "File"),
        Fixed("フリップする", "Flip"),
        Fixed("フリーカメラを使用する", "Use Free Camera"),
        Fixed("フレームの長さ", "Frame Length"),
        Fixed("フレーム毎のトランジション", "Per-Frame Transition"),
        Fixed("プロシージャルヘルパー（未実装）", "Procedural Helper (Not Implemented)"),
        Fixed("プロシージャル編集", "Procedural Editor"),
        Fixed("ポーズのプロシージャル編集を補助します。\n手先や足先以外に使用すると不自然な結果になる場合があります。\nこの機能は未完成です。", "Assists with procedural pose editing.\nUsing it on parts other than hands or feet may produce unnatural results.\nThis feature is incomplete."),
        Fixed("リロードする", "Reload"),
        Fixed("ループ再生", "Loop Playback"),
        Fixed("ローカル", "Local"),
        Fixed("一時停止", "Pause"),
        Fixed("位置", "Position"),
        Fixed("保存していない項目はリセットされます。\n新しくROBOを開きますか？", "Unsaved changes will be reset.\nOpen another ROBO?"),
        Fixed("全アニメに適用", "Apply to All Animations"),
        Fixed("全値をコピー", "Copy All Values"),
        Fixed("全値を貼り付け", "Paste All Values"),
        Fixed("再生", "Play"),
        Fixed("制約の詳細を表示する", "Show Constraint Details"),
        Fixed("前後左右ステップのアニメ", "Directional Step Animations"),
        Fixed("各特殊技アニメ", "Special Move Animations"),
        Fixed("回転", "Rotation"),
        Fixed("回転を制約に同期する", "Synchronize Rotation to Constraint"),
        Fixed("変更を保存する", "Save Changes"),
        Fixed("大きさ", "Scale"),
        Fixed("座標系の種類", "Coordinate Space"),
        Fixed("必殺技（109番）", "Finisher (No. 109)"),
        Fixed("指定アニメに適用", "Apply to Selected Animation"),
        Fixed("新しいHODを追加", "Add New HOD"),
        Fixed("新しくROBOを開く", "Open Another ROBO"),
        Fixed("特殊アニメ", "Special Animations"),
        Fixed("画面モード選択", "View Mode"),
        Fixed("終了する", "Exit"),
        Fixed("編集", "Edit"),
        Fixed("編集を終了する", "Finish Editing"),
        Fixed("編集モード", "Edit Mode"),
        Fixed("表示／非表示", "Show/Hide"),
        Fixed("補助のオン／オフ", "Enable/Disable Helper"),
        Fixed("視点の回転速度", "View Rotation Speed"),
        Fixed("視点を初期位置に戻す", "Reset View"),
        Fixed("設定を完了する", "Apply Settings"),
        Fixed("環境設定", "Configuration"),
        Fixed("読み込む機体を選択する", "Select a Mecha to Load"),
        Fixed("選択HODをコピー", "Copy Selected HOD"),
        Fixed("選択HODを上に移動", "Move Selected HOD Up"),
        Fixed("選択HODを下に移動", "Move Selected HOD Down"),
        Fixed("選択HODを削除", "Delete Selected HOD"),
        Fixed("選択Scriptを上に移動", "Move Selected Script Up"),
        Fixed("選択Scriptを下に移動", "Move Selected Script Down"),
        Fixed("選択Scriptを削除", "Delete Selected Script"),
        Fixed("選択アニメ再生", "Play Selected Animation"),
        Fixed("選択ツリーに新しいパーツを追加します。", "Add a new part to the selected tree location."),
        Fixed("選択パーツの名前を変更", "Rename Selected Part"),
        Fixed("選択パーツの情報をコピー", "Copy Selected Part Data"),
        Fixed("選択パーツの情報をペースト", "Paste Selected Part Data"),
        Fixed("選択パーツを\n削除", "Delete\nSelected Part"),
        Fixed("選択パーツをターゲットにする", "Target Selected Part"),
        Fixed("選択パーツを削除", "Delete Selected Part"),
        Fixed("選択パーツ値を全てのHODに適用する", "Apply Selected Part Values to All HODs"),
        Fixed("選択位置に\nパーツ追加", "Add Part at\nSelected Position"),
        Fixed("選択位置にScript追加", "Add Script at Selected Position"),
        Fixed("選択位置にパーツを追加", "Add Part at Selected Position"),
        Fixed("言語", "Language")
    };

    static readonly Translation[] DynamicTranslations =
    {
        Dynamic("status.loading.complete", "読み込み完了", "Loading complete."),
        Dynamic("status.loading.progress", "読み込み中...{0}%", "Loading...{0}%"),
        Dynamic("dialog.no_mecha_data", "ディレクトリ内に機体データがみつかりません。", "No mecha data was found in the directory."),
        Dynamic("dialog.no_mecha_data_found", "ディレクトリ内に機体データがみつかりませんでした。", "No mecha data was found in the directory."),
        Dynamic("dialog.initial_directory_created", "初期ディレクトリ（Windom_Data\\Robo）を作成しました。", "Created the default directory (Windom_Data\\Robo)."),
        Dynamic("dialog.mecha_load_failed", "機体データの読み込みに失敗しました。\n{0}", "Failed to load mecha data.\n{0}"),
        Dynamic("dialog.hod_new_name", "新しいHODファイルの名前を入力してください（拡張子.hodは除く）。", "Enter a name for the new HOD file (without the .hod extension)."),
        Dynamic("dialog.hod_select", "読み込むHODファイルを選択してください", "Select the HOD file to load."),
        Dynamic("dialog.hod_not_found", "フォルダ内にHODファイルが見つかりません: {0}", "No HOD files were found in the folder: {0}"),
        Dynamic("dialog.hod_load_failed", "HODファイルの読み込みに失敗しました: {0}", "Failed to load the HOD file: {0}"),
        Dynamic("dialog.hod_part_count_mismatch", "読み込んだHODファイルのパーツ数({0})が現在のパーツ数({1})と異なります。読み込みを中止します。", "The loaded HOD contains {0} parts, but the current mecha contains {1}. Loading was canceled."),
        Dynamic("dialog.hod_load_exception", "HODファイルの読み込み中にエラーが発生しました: {0}", "An error occurred while loading the HOD file: {0}"),
        Dynamic("dialog.animation_new_name", "新しいアニメーション名を入力してください", "Enter a new animation name."),
        Dynamic("dialog.hod_no_frames", "このアニメーションにHODファイルはありません。", "This animation does not contain any HOD frames."),
        Dynamic("dialog.hod_rename", "HODファイルを改名します。新しい名前を入力してください。", "Enter a new name for the HOD file."),
        Dynamic("dialog.apply_selected_part_all_hods", "選択パーツの値を全てのHODに適用します。", "Apply the selected part values to every HOD."),
        Dynamic("dialog.apply_selected_value_all_hods", "選択値を全てのHODに適用します。", "Apply the selected value to every HOD."),
        Dynamic("dialog.animation_sync_range", "同期させたいアニメーションをカンマで区切って入力してください。2つのアニメーションの間にダッシュを入れると、範囲を指定できます。  例: 1,2, 6-10", "Enter animation numbers separated by commas. Use a dash to specify a range. Example: 1,2, 6-10"),
        Dynamic("dialog.select_part_to_delete", "削除するパーツを選択してください。", "Select a part to delete."),
        Dynamic("dialog.root_part_cannot_delete", "ルートパーツは削除できません。", "The root part cannot be deleted."),
        Dynamic("dialog.part_data_invalid_delete", "パーツ情報の整合性を確認できないため削除できません。", "The part cannot be deleted because its data integrity could not be verified."),
        Dynamic("dialog.part_select_rename", "どのパーツの名前を変更しますか？", "Which part do you want to rename?"),
        Dynamic("dialog.part_missing_use_empty", "指定名のパーツはフォルダに存在しません。空のパーツと入れ替えます。", "The specified part does not exist in the folder. It will be replaced with an empty part."),
        Dynamic("dialog.add_part_under", "{0}の下に新規パーツを追加します。", "Add a new part under {0}."),
        Dynamic("dialog.delete_part_with_children", "子パーツごと削除しますがよろしいですか？", "Delete the selected part and all of its child parts?"),
        Dynamic("dialog.delete_selected_part", "選択パーツを削除します。", "Delete the selected part."),
        Dynamic("default.hod_new_name", "新規HODファイル", "New HOD File"),
        Dynamic("help.application_version", "アプリケーションバージョン:Ver.{0}", "Application Version: {0}")
    };

    [MenuItem("Tools/WindomXP/Localization/日英Localizationを構築")]
public static void BuildJapaneseEnglishLocalization()
    {
        EnsureFolders();
        LocalizationSettings settings = EnsureLocalizationSettings();
        Locale japanese = EnsureLocale("ja", SystemLanguage.Japanese, "Japanese (ja)");
        Locale english = EnsureLocale("en", SystemLanguage.English, "English (en)");
        LocalizationSettings.ProjectLocale = japanese;
        EditorUtility.SetDirty(settings);

        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(UILocalization.TableName);
        if (collection == null)
        {
            collection = LocalizationEditorSettings.CreateStringTableCollection(
                UILocalization.TableName,
                TableRoot,
                new List<Locale> { japanese, english });
        }

        StringTable japaneseTable = collection.GetTable(japanese.Identifier) as StringTable;
        StringTable englishTable = collection.GetTable(english.Identifier) as StringTable;
        AddOrUpdateEntries(japaneseTable, englishTable, FixedTranslations);
        AddOrUpdateEntries(japaneseTable, englishTable, DynamicTranslations);
        LocalizationEditorSettings.SetPreloadTableFlag(japaneseTable, true);
        LocalizationEditorSettings.SetPreloadTableFlag(englishTable, true);

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int localizedCount = AttachFixedTextLocalizers(scene);
        CreateLanguageSelector(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[LocalizationProjectSetup] 日英Localizationを構築しました。固定UI: {localizedCount}件");
    }

    static void AddOrUpdateEntries(StringTable japaneseTable, StringTable englishTable, IEnumerable<Translation> translations)
    {
        foreach (Translation translation in translations)
        {
            StringTableEntry japaneseEntry = japaneseTable.GetEntry(translation.Key) ?? japaneseTable.AddEntry(translation.Key, translation.Japanese);
            StringTableEntry englishEntry = englishTable.GetEntry(translation.Key) ?? englishTable.AddEntry(translation.Key, translation.English);
            japaneseEntry.Value = translation.Japanese;
            englishEntry.Value = translation.English;
        }

        EditorUtility.SetDirty(japaneseTable);
        EditorUtility.SetDirty(englishTable);
        EditorUtility.SetDirty(japaneseTable.SharedData);
    }

    static int AttachFixedTextLocalizers(Scene scene)
    {
        int count = 0;
        foreach (Text textComponent in Resources.FindObjectsOfTypeAll<Text>())
        {
            if (textComponent == null || textComponent.gameObject.scene != scene)
                continue;

            Translation translation = FindFixedTranslation(textComponent.text);
            if (translation == null)
                continue;

            UILocalizedText localizer = textComponent.GetComponent<UILocalizedText>();
            if (localizer == null)
                localizer = Undo.AddComponent<UILocalizedText>(textComponent.gameObject);

            SerializedObject serializedLocalizer = new SerializedObject(localizer);
            serializedLocalizer.FindProperty("entryKey").stringValue = translation.Key;
            serializedLocalizer.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(localizer);
            count++;
        }

        return count;
    }

    static Translation FindFixedTranslation(string value)
    {
        foreach (Translation translation in FixedTranslations)
        {
            if (translation.Matches(value))
                return translation;
        }

        return null;
    }

static void CreateLanguageSelector(Scene scene)
    {
        Transform overlay = FindInScene(scene, "UI_Overlay");
        Transform prefPanel = overlay == null ? null : overlay.Find("PrefPanel");
        if (prefPanel == null)
            throw new InvalidOperationException("PrefPanelが見つかりません。");

        Transform existingDropdown = prefPanel.Find("LanguageDropdown");
        GameObject dropdownObject;
        if (existingDropdown == null)
        {
            Transform source = overlay.Find("メニューバー/ModeSelect/Dropdown");
            if (source == null)
                throw new InvalidOperationException("複製元Dropdownが見つかりません。");

            dropdownObject = UnityEngine.Object.Instantiate(source.gameObject, prefPanel, false);
            dropdownObject.name = "LanguageDropdown";
            Undo.RegisterCreatedObjectUndo(dropdownObject, "Create language dropdown");
        }
        else
        {
            dropdownObject = existingDropdown.gameObject;
        }

        RectTransform dropdownRect = dropdownObject.GetComponent<RectTransform>();
        dropdownRect.anchorMin = new Vector2(0f, 1f);
        dropdownRect.anchorMax = new Vector2(0f, 1f);
        dropdownRect.pivot = new Vector2(0.5f, 0.5f);
        dropdownRect.anchoredPosition = new Vector2(390f, -160f);
        dropdownRect.sizeDelta = new Vector2(230f, 30f);

        foreach (UILocalizedText inheritedLocalizer in dropdownObject.GetComponentsInChildren<UILocalizedText>(true))
            Undo.DestroyObjectImmediate(inheritedLocalizer);

        Dropdown dropdown = dropdownObject.GetComponent<Dropdown>();
        dropdown.onValueChanged = new Dropdown.DropdownEvent();
        dropdown.ClearOptions();
        dropdown.AddOptions(new List<string> { "日本語", "English" });
        dropdown.SetValueWithoutNotify(0);
        if (dropdownObject.GetComponent<UILanguageSelector>() == null)
            Undo.AddComponent<UILanguageSelector>(dropdownObject);

        Transform existingLabel = prefPanel.Find("LanguageLabel");
        GameObject labelObject;
        if (existingLabel == null)
        {
            Text sourceLabel = null;
            foreach (Text text in prefPanel.GetComponentsInChildren<Text>(true))
            {
                if (Normalize(text.text) == "ROBOフォルダーのディレクトリパス")
                {
                    sourceLabel = text;
                    break;
                }
            }

            if (sourceLabel == null)
                throw new InvalidOperationException("複製元ラベルが見つかりません。");

            labelObject = UnityEngine.Object.Instantiate(sourceLabel.gameObject, prefPanel, false);
            labelObject.name = "LanguageLabel";
            Undo.RegisterCreatedObjectUndo(labelObject, "Create language label");
        }
        else
        {
            labelObject = existingLabel.gameObject;
        }

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = new Vector2(165f, -160f);
        labelRect.sizeDelta = new Vector2(277f, 26f);

        Text label = labelObject.GetComponent<Text>();
        label.text = "言語";
        Translation translation = FindFixedTranslation("言語");
        UILocalizedText localizer = labelObject.GetComponent<UILocalizedText>();
        if (localizer == null)
            localizer = Undo.AddComponent<UILocalizedText>(labelObject);

        SerializedObject serializedLocalizer = new SerializedObject(localizer);
        serializedLocalizer.FindProperty("entryKey").stringValue = translation.Key;
        serializedLocalizer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(localizer);
    }

    static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == name)
                return root.transform;
        }

        return null;
    }

    static Locale EnsureLocale(string code, SystemLanguage language, string assetName)
    {
        Locale locale = LocalizationEditorSettings.GetLocale(code);
        if (locale != null)
            return locale;

        locale = Locale.CreateLocale(language);
        AssetDatabase.CreateAsset(locale, LocaleRoot + "/" + assetName + ".asset");
        LocalizationEditorSettings.AddLocale(locale);
        return locale;
    }

    static void EnsureFolders()
    {
        EnsureFolder(LocalizationRoot);
        EnsureFolder(LocaleRoot);
        EnsureFolder(TableRoot);
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        string name = path.Substring(separator + 1);
        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    static string Normalize(string value)
    {
        return (value ?? string.Empty)
            .Replace("１", "1")
            .Replace("２", "2")
            .TrimEnd('\r', '\n');
    }


static LocalizationSettings EnsureLocalizationSettings()
    {
        const string settingsPath = LocalizationRoot + "/Localization Settings.asset";
        LocalizationSettings settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings == null)
        {
            settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(settingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<LocalizationSettings>();
                settings.name = "WindomXP Localization Settings";
                AssetDatabase.CreateAsset(settings, settingsPath);
            }

            LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        }

        return settings;
    }
}
