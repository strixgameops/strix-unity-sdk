using StrixSDK.Runtime.Utils;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

namespace StrixSDK.Runtime
{
    public class LocalizationManager : MonoBehaviour
    {
        private static LocalizationManager _instance;

        public static LocalizationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<LocalizationManager>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<LocalizationManager>();
                        obj.name = typeof(LocalizationManager).ToString();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void Initialize()
        {
            //StartCoroutine(UpdateLocalization());
        }

        // TODO: Localization table auto update
        //private IEnumerator UpdateLocalization()
        //{
        //    var localizationDocuments = Content.LoadAllFromFile("localization");
        //    if (localizationDocuments == null)
        //    {
        //        Debug.LogError($"No localization items found");
        //        yield break;
        //    }

        //    var table = LocalizationSettings.StringDatabase.GetTableAsync("My Table");
        //    yield return table;

        //    Addressables.ResourceManager.Acquire(table);

        //    var stringTable = table.Result;
        //    var entry = stringTable.GetEntry("Some Entry");
        //    entry.Value = "Hello World";

        //    var newEntry = stringTable.AddEntry("New Entry", "Localized Value");

        //    M_LocalizationItem[] items = ConvertListToArray<M_LocalizationItem>(localizationDocuments);
        //    foreach (var item in items)
        //    {
        //        foreach (var translation in item.Items)
        //        {
        //            yield return AddOrUpdateLocalizationEntry(item.Key, translation.Code, translation.Value);
        //        }
        //    }

        //    LocalizationSettings.StringDatabase.RefreshTableContents();
        //}

        //private static T[] ConvertListToArray<T>(List<dynamic> dynamicList)
        //{
        //    return dynamicList.Cast<T>().ToArray();
        //}
        public string GetLocalizedString(string key)
        {
            M_LocalizationItem localizationItem = Content.LoadFromFile<M_LocalizationItem>("localization", key);
            if (localizationItem == null)
            {
                Debug.LogError($"No localization item found with key {key}");
            }
            Locale currentSelectedLocale = LocalizationSettings.SelectedLocale;
            TranslationItem translationItem = localizationItem.Items.FirstOrDefault(i => i.Code == currentSelectedLocale.Identifier.Code);
            if (translationItem == null)
            {
                // Fallback to default language (English)
                translationItem = localizationItem.Items.FirstOrDefault(i => i.Code == "en");
            }
            if (translationItem == null)
            {
                // If for some reason still null, return error.
                Debug.LogError($"No translation found for locale '{currentSelectedLocale.Identifier.Code}'");
                return key;
            }
            return translationItem.Value;
        }

        public string GetLocalizedStringByLocale(string key, string locale)
        {
            M_LocalizationItem localizationItem = Content.LoadFromFile<M_LocalizationItem>("localization", key);
            if (localizationItem == null)
            {
                Debug.LogError($"No localization item found with key {key}");
            }
            TranslationItem translationItem = localizationItem.Items.FirstOrDefault(i => i.Code == locale);
            if (translationItem == null)
            {
                Debug.Log($"No translation found for locale '{locale}'. Fallback to 'en' locale was made...");
                // Fallback to default language (English)
                translationItem = localizationItem.Items.FirstOrDefault(i => i.Code == "en");
            }
            if (translationItem == null)
            {
                // If for some reason still null, return error.
                Debug.LogError($"No translation found for locale '{locale}'");
                return "undefined";
            }
            return translationItem.Value;
        }
    }
}