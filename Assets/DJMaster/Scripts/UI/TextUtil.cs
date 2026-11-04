using System.Text;

namespace DJMaster
{
    public static class TextUtil
    {
        // TextMesh has no word wrap, so break lines by character count.
        public static string Wrap(string text, int maxChars)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            int line = 0;
            foreach (var word in text.Split(' '))
            {
                if (line > 0 && line + 1 + word.Length > maxChars) { sb.Append('\n'); line = 0; }
                else if (line > 0) { sb.Append(' '); line++; }
                sb.Append(word);
                line += word.Length;
            }
            return sb.ToString();
        }

        public static string Clock(float seconds)
        {
            if (seconds < 0) seconds = 0;
            int s = (int)seconds;
            return $"{s / 60}:{s % 60:00}";
        }
    }
}
