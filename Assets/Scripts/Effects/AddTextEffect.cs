using UnityEngine;

namespace DefaultNamespace.Effects
{
    /// <summary>
    /// Effect to add custom text to effectResultString
    /// </summary>
    public class AddTextEffect : EffectScript
    {
        [Tooltip("Text content to add to effectResultString")]
        [TextArea(3, 10)]
        public string textToAdd = "";
        
        [Tooltip("Whether to automatically add newline at end of text")]
        public bool addNewLine = true;

        /// <summary>
        /// Add text to effectResultString
        /// </summary>
        public void AddText()
        {
            if (string.IsNullOrEmpty(textToAdd))
                return;
                
            AppendLog(textToAdd);
        }
        
        /// <summary>
        /// Add text with card name prefix (automatically colored based on card ownership)
        /// </summary>
        public void AddTextWithCardPrefix()
        {
            if (string.IsNullOrEmpty(textToAdd))
                return;

            string myColor = myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef ? "#87CEEB" : "orange";
            AppendLog($"// [<color={myColor}>" + myCard.name + $"</color>] {textToAdd}");
        }
        
        /// <summary>
        /// Dynamically set and add text
        /// </summary>
        public void AddTextDynamic(string dynamicText)
        {
            if (string.IsNullOrEmpty(dynamicText))
                return;
                
            AppendLog(dynamicText);
        }
    }
}
