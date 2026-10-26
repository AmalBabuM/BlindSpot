using UnityEngine;

namespace Clearance
{
    public static class FieldUi
    {
        public static readonly Color Surface = new Color(.09f, .065f, .075f, .97f);
        public static readonly Color Muted = new Color(.79f, .73f, .74f);
        public static readonly Color Accent = new Color32(240, 107, 104, 255);
        public static readonly Color Success = new Color(.39f, .91f, .66f);
        public static GUIStyle Title, Body, Small, Eyebrow, ButtonText;
        public static float Opacity = 1;
        public static void Prepare()
        {
            if (Title != null) return;
            Title = Style(24, FontStyle.Bold, Color.white);
            Body = Style(17, FontStyle.Normal, new Color(.94f, .90f, .90f)); Body.wordWrap = true;
            Small = Style(14, FontStyle.Normal, Muted); Small.wordWrap = true;
            Eyebrow = Style(11, FontStyle.Bold, Accent);
            ButtonText = Style(15, FontStyle.Bold, Color.white); ButtonText.alignment = TextAnchor.MiddleCenter;
            ButtonText.normal.background = null;
        }
        static GUIStyle Style(int size, FontStyle weight, Color color)
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = weight };
            style.normal.textColor = color; return style;
        }
        public static void Rounded(Rect rect, Color color, float radius = 14)
        {
            color.a *= Opacity;
            // The explicit tint of this draw overload is linear in a Linear project.
            // Palette values are authored as display colours, like the web menu CSS.
            if (QualitySettings.activeColorSpace == ColorSpace.Linear) color = color.linear;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, radius);
        }
        public static void Panel(Rect rect)
        {
            Rounded(new Rect(rect.x, rect.y + 3, rect.width, rect.height), new Color(0, 0, 0, .2f));
            Rounded(rect, Surface);
        }
        public static bool Button(Rect rect, string label, bool primary = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Rounded(rect, primary ? (hover ? new Color32(201, 65, 77, 255) : new Color32(184, 50, 61, 255)) :
                hover ? new Color(.27f, .18f, .20f) : new Color(.17f, .12f, .14f), 10);
            return GUI.Button(rect, label, ButtonText);
        }
    }
}
