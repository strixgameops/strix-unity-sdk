using StrixSDK.Runtime;
using System.Threading.Tasks;
using UnityEngine;

using System.Collections.Generic;
using System;

namespace StrixSDK
{
    public class Localization : MonoBehaviour
    {
        #region References

        private static Localization _instance;

        public static Localization Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<Localization>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<Localization>();
                        obj.name = typeof(Localization).ToString();
                    }
                }
                return _instance;
            }
        }

        private void Start()
        {
        }

        #endregion References

        #region Static methods

        /// <summary>
        /// Get localized text by it's key. Uses LocalizationSettings.SelectedLocale to get current locale.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static string GetLocalizedString(string key)
        {
            return Instance.I_GetLocalizedString(key);
        }

        /// <summary>
        /// Get localized text by it's key and locale. If wasn't found, fallback to "en" locale.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="locale"></param>
        /// <returns></returns>
        public static string GetLocalizedStringByLocale(string key, string locale)
        {
            return Instance.I_GetLocalizedStringByLocale(key, locale);
        }

        #endregion Static methods

        #region Instance methods

        private string I_GetLocalizedString(string key)
        {
            return LocalizationManager.Instance.GetLocalizedString(key);
        }

        private string I_GetLocalizedStringByLocale(string key, string locale)
        {
            return LocalizationManager.Instance.GetLocalizedStringByLocale(key, locale);
        }

        #endregion Instance methods
    }
}