using System.Globalization;
using System.Text;

namespace NetrunnerSolver.Core.Text;

// 로그 기록 한 건을 만든다. 기록은 언제나 시각으로 시작하는 줄 하나로 시작한다.
// 메시지 안의 줄바꿈(CR·LF·NEL·LS·PS)은 들여 쓴 이어지는 줄로 바꿔 예외 스택처럼 여러 줄인 내용도 읽히게 두고,
// 그 밖의 제어·서식 문자는 \uXXXX로 적는다. 그래서 메시지에 무엇이 들어와도 새 기록처럼 보이는 줄을 만들 수 없다.
public static class LogText
{
    public const string Continuation = "    ";

    public static string Line(DateTime time, string level, string message)
    {
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(message);
        var builder = new StringBuilder(message.Length + 32);
        builder.Append(time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture)).Append(' ');
        AppendEscaped(builder, level);
        builder.Append(' ');
        AppendEscaped(builder, message);
        return builder.Append(Environment.NewLine).ToString();
    }

    private static void AppendEscaped(StringBuilder builder, string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c is '\r' or '\n' or '\u0085' or '\u2028' or '\u2029')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                builder.Append(Environment.NewLine).Append(Continuation);
            }
            else if (char.IsControl(c) || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format)
            {
                builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}");
            }
            else
            {
                builder.Append(c);
            }
        }
    }
}
