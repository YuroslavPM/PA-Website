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

    public static string ToSlug(this string text)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;

        // Bulgarian Cyrillic to Latin mapping
        var transliterationMap = new Dictionary<char, string>
        {
            {'а', "a"}, {'б', "b"}, {'в', "v"}, {'г', "g"}, {'д', "d"}, {'е', "e"}, {'ж', "zh"}, {'з', "z"},
            {'и', "i"}, {'й', "y"}, {'к', "k"}, {'л', "l"}, {'м', "m"}, {'н', "n"}, {'о', "o"}, {'п', "p"},
            {'р', "r"}, {'с', "s"}, {'т', "t"}, {'у', "u"}, {'ф', "f"}, {'х', "h"}, {'ц', "ts"}, {'ч', "ch"},
            {'ш', "sh"}, {'щ', "sht"}, {'ъ', "a"}, {'ь', "y"}, {'ю', "yu"}, {'я', "ya"}
        };

        text = text.ToLowerInvariant();
        var sb = new System.Text.StringBuilder();

        foreach (char c in text)
        {
            if (transliterationMap.TryGetValue(c, out var latin))
            {
                sb.Append(latin);
            }
            else if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
            {
                sb.Append(c);
            }
            else if (c == ' ' || c == '-' || c == '_')
            {
                sb.Append('-');
            }
        }

        string slug = sb.ToString();

        // Clean up: remove multiple hyphens, leading/trailing hyphens
        slug = Regex.Replace(slug, @"-+", "-");
        slug = slug.Trim('-');

        return slug;
    }
}
