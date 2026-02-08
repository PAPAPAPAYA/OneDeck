using UnityEngine;

namespace DefaultNamespace.Effects
{
    /// <summary>
    /// 向 effectResultString 添加自定义文字的 Effect
    /// </summary>
    public class AddTextEffect : EffectScript
    {
        [Tooltip("要添加到 effectResultString 的文字内容")]
        [TextArea(3, 10)]
        public string textToAdd = "";
        
        [Tooltip("是否在文字末尾自动添加换行符")]
        public bool addNewLine = true;

        /// <summary>
        /// 添加文字到 effectResultString
        /// </summary>
        public void AddText()
        {
            if (string.IsNullOrEmpty(textToAdd))
                return;
                
            effectResultString.value += textToAdd + (addNewLine ? "\n" : "");
        }
        
        /// <summary>
        /// 添加带卡片名称前缀的文字（自动根据卡片归属着色）
        /// </summary>
        public void AddTextWithCardPrefix()
        {
            if (string.IsNullOrEmpty(textToAdd))
                return;

            string myColor = myCardScript.myStatusRef == CombatManager.Me.ownerPlayerStatusRef ? "#87CEEB" : "orange";
            effectResultString.value += $"// [<color={myColor}>" + myCard.name + $"</color>] {textToAdd}" + (addNewLine ? "\n" : "");
        }
        
        /// <summary>
        /// 动态设置并添加文字
        /// </summary>
        public void AddTextDynamic(string dynamicText)
        {
            if (string.IsNullOrEmpty(dynamicText))
                return;
                
            effectResultString.value += dynamicText + (addNewLine ? "\n" : "");
        }
    }
}
