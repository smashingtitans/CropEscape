using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

namespace HappyHarvest
{
    public class WinScreen : MonoBehaviour
    {
        private const string FullMessage = "Congratulations!\n\nYou managed to sell enough crops,\nyou can now move to the city.";
        private const float CharDelay     = 0.05f;
        private const float FadeInDuration = 1.5f;

        private UIDocument    m_Document;
        private Label         m_WinMessage;
        private VisualElement m_Blocker;

        void Start()
        {
            m_Document   = GetComponent<UIDocument>();
            m_WinMessage = m_Document.rootVisualElement.Q<Label>("WinMessage");
            m_Blocker    = m_Document.rootVisualElement.Q<VisualElement>("Blocker");

            if (m_WinMessage != null) m_WinMessage.text = "";
            if (m_Blocker    != null) m_Blocker.style.opacity = 1f;

            StartCoroutine(PlaySequence());
        }

        IEnumerator PlaySequence()
        {
            // Fade in from black
            float elapsed = 0f;
            while (elapsed < FadeInDuration)
            {
                elapsed += Time.deltaTime;
                if (m_Blocker != null)
                    m_Blocker.style.opacity = 1f - Mathf.Clamp01(elapsed / FadeInDuration);
                yield return null;
            }
            if (m_Blocker != null) m_Blocker.style.opacity = 0f;

            yield return new WaitForSeconds(0.3f);

            // Typewriter effect
            for (int i = 0; i <= FullMessage.Length; i++)
            {
                if (m_WinMessage != null)
                    m_WinMessage.text = FullMessage.Substring(0, i);
                yield return new WaitForSeconds(CharDelay);
            }
        }
    }
}
