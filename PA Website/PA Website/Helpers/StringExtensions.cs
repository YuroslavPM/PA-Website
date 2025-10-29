namespace PA_Website.Helpers;
using System.Text.RegularExpressions;

public static class StringExtensions
{
    public static string TruncateHtml(this string html, int maxLength)
    {
        if (string.IsNullOrEmpty(html))
            return html;

        // First remove all HTML tags to get plain text length
        var plainText = Regex.Replace(html, "<.*?>", string.Empty);

        if (plainText.Length <= maxLength)
            return html; // No need to truncate

        var truncated = string.Empty;
        var tagStack = new Stack<string>();
        var currentLength = 0;
        var regex = new Regex("(<[^>]+>)|([^<]+)");
        var matches = regex.Matches(html);

        foreach (Match match in matches)
        {
            if (match.Value.StartsWith("<"))
            {
                // It's a tag
                truncated += match.Value;

                if (match.Value.StartsWith("</"))
                {
                    // Closing tag — pop only if stack not empty
                    if (tagStack.Count > 0)
                        tagStack.Pop();
                }
                else if (!match.Value.EndsWith("/>"))
                {
                    // Opening tag (not self-closing)
                    var tagName = match.Value.Split(new[] { ' ', '>' }, StringSplitOptions.RemoveEmptyEntries)[0].Substring(1);
                    tagStack.Push(tagName);
                }
            }
            else
            {
                // It's text content
                var text = match.Value;
                if (currentLength + text.Length > maxLength)
                {
                    var remaining = maxLength - currentLength;
                    truncated += text.Substring(0, remaining) + "...";
                    currentLength = maxLength;
                    break;
                }

                truncated += text;
                currentLength += text.Length;
            }
        }

        // Close any remaining open tags
        while (tagStack.Count > 0)
        {
            truncated += $"</{tagStack.Pop()}>";
        }

        return truncated;
    }
}
