using UnityEngine;

namespace Clearance
{
    public static class FieldUi
    {
        public static readonly Color Surface = new Color(.055f, .078f, .105f, .97f);
        public static readonly Color Muted = new Color(.66f, .73f, .81f);
        public static readonly Color Accent = new Color(.23f, .83f, .79f);
        public static readonly Color Success = new Color(.39f, .91f, .66f);
        public static GUIStyle Title, Body, Small, Eyebrow, ButtonText;
        public static float Opacity = 1;
        public static void Prepare()
        {
            if (Title != null) return;
            Title = Style(24, FontStyle.Bold, Color.white);
            Body = Style(17, FontStyle.Normal, new Color(.86f, .90f, .94f)); Body.wordWrap = true;
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
        { color.a *= Opacity; GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, radius); }
        public static void Panel(Rect rect)
        {
            Rounded(new Rect(rect.x, rect.y + 3, rect.width, rect.height), new Color(0, 0, 0, .2f));
            Rounded(rect, Surface);
        }
        public static bool Button(Rect rect, string label, bool primary = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Rounded(rect, primary ? (hover ? new Color(.12f, .56f, .55f) : new Color(.08f, .42f, .43f)) :
                hover ? new Color(.19f, .25f, .32f) : new Color(.12f, .17f, .23f), 10);
            return GUI.Button(rect, label, ButtonText);
        }
    }
}
