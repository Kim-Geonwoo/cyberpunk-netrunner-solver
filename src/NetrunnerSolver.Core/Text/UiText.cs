namespace NetrunnerSolver.Core.Text;

public static class UiText
{
    private static readonly Dictionary<string, string> Korean = new(StringComparer.Ordinal)
    {
        ["status.allSolvable"] = "모두 풀 수 있음",
        ["status.partial"] = "일부만 가능 {0}/{1}",
        ["status.none"] = "풀 수 있는 시퀀스 없음",
        ["status.notFound"] = "퍼즐 화면을 찾지 못함",
        ["status.cursorCovering"] = "마우스 커서가 코드를 가렸습니다",
        ["hint.moveCursor"] = "커서를 행렬 밖으로 옮긴 뒤 다시 인식하세요",
        ["status.timedOut"] = "시간 초과, 지금까지 찾은 최선",
        ["seq.done"] = "성공",
        ["seq.fail"] = "실패",
        ["label.solution"] = "정답",
        ["label.buffer"] = "버퍼",
        ["label.auto"] = "자동",
        ["action.reread"] = "다시 인식",
        ["action.saveCapture"] = "캡처 저장",
        ["action.close"] = "닫기",
        ["warn.unknownCells"] = "못 읽은 칸이 있음, 작은 행렬에서 눌러 고치세요",
        ["warn.bufferNotDetected"] = "버퍼 크기를 못 읽어 마지막 값 사용",
        ["warn.bufferHasContent"] = "이미 고른 코드가 있음, 처음 상태 기준 풀이",
        ["warn.noSequences"] = "시퀀스를 못 읽음",
        ["warn.fullscreen"] = "안 보이면 게임을 테두리 없는 창으로",
        ["tray.title"] = "넷러너 풀이기",
        ["tray.started"] = "{0} 를 누르면 풀이",
        ["tray.hotkey"] = "단축키 바꾸기…",
        ["tray.language"] = "언어",
        ["tray.languageAuto"] = "자동",
        ["tray.captures"] = "캡처 폴더 열기",
        ["tray.exit"] = "끝내기",
        ["hotkey.title"] = "단축키 바꾸기",
        ["hotkey.prompt"] = "새 조합을 누르세요(키 1~3개)",
        ["hotkey.save"] = "저장",
        ["hotkey.cancel"] = "취소",
    };

    private static readonly Dictionary<string, string> English = new(StringComparer.Ordinal)
    {
        ["status.allSolvable"] = "All sequences solvable",
        ["status.partial"] = "Partial {0}/{1}",
        ["status.none"] = "No sequence solvable",
        ["status.notFound"] = "Breach screen not found",
        ["status.cursorCovering"] = "The mouse cursor is covering a code",
        ["hint.moveCursor"] = "Move the cursor off the grid, then re-scan",
        ["status.timedOut"] = "Timed out, best found so far",
        ["seq.done"] = "OK",
        ["seq.fail"] = "Fail",
        ["label.solution"] = "Solution",
        ["label.buffer"] = "Buffer",
        ["label.auto"] = "auto",
        ["action.reread"] = "Re-scan",
        ["action.saveCapture"] = "Save capture",
        ["action.close"] = "Close",
        ["warn.unknownCells"] = "Some cells unreadable, click them in the mini grid",
        ["warn.bufferNotDetected"] = "Buffer size not detected, using last value",
        ["warn.bufferHasContent"] = "Buffer not empty, solving from the start",
        ["warn.noSequences"] = "No sequences found",
        ["warn.fullscreen"] = "If hidden, set the game to borderless window",
        ["tray.title"] = "Netrunner Solver",
        ["tray.started"] = "Press {0} to solve",
        ["tray.hotkey"] = "Change hotkey…",
        ["tray.language"] = "Language",
        ["tray.languageAuto"] = "Auto",
        ["tray.captures"] = "Open captures folder",
        ["tray.exit"] = "Exit",
        ["hotkey.title"] = "Change hotkey",
        ["hotkey.prompt"] = "Press the new combination (1–3 keys)",
        ["hotkey.save"] = "Save",
        ["hotkey.cancel"] = "Cancel",
    };

    public static string Get(string key, string language)
    {
        ArgumentNullException.ThrowIfNull(key);
        var table = Table(language);
        return table.TryGetValue(key, out var value) ? value : throw new KeyNotFoundException($"UI text key not found: {key}");
    }

    public static IReadOnlyCollection<string> Keys(string language) => Table(language).Keys;

    public static string ResolveLanguage(string setting, string uiCultureName)
    {
        if (setting is "ko" or "en")
        {
            return setting;
        }

        return uiCultureName?.StartsWith("ko", StringComparison.OrdinalIgnoreCase) == true ? "ko" : "en";
    }

    private static Dictionary<string, string> Table(string language) => language == "ko" ? Korean : English;
}
