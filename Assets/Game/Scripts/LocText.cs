using UnityEngine;
using UnityEngine.UI;

namespace TinyRpg
{
    /// Componente para textos de UI localizados: fija el texto segun la clave
    /// y se refresca al cambiar el idioma.
    public class LocText : MonoBehaviour
    {
        public string key;

        Text uiText;

        void Awake()
        {
            uiText = GetComponent<Text>();
        }

        void OnEnable()
        {
            Refresh();
            Loc.LanguageChanged += Refresh;
        }

        void OnDisable()
        {
            Loc.LanguageChanged -= Refresh;
        }

        void Refresh()
        {
            if (uiText != null && !string.IsNullOrEmpty(key))
                uiText.text = Loc.T(key);
        }
    }
}
