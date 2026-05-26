using TypeRacer.Shared;

namespace TypeRacer.Shared.Payloads.Room;

public class CreateRoomRequest
{
    /// <summary>
    /// Ngôn ngữ passage cho phòng: "en", "vi", hoặc "any".
    /// </summary>
    public string PassageLanguage { get; set; } = "en";

    /// <summary>Thời gian đua (giây). Nếu <= 0 sẽ dùng mặc định.</summary>
    public int RaceDurationSeconds { get; set; }

    /// <summary>Tạo bài tập dựa trên sai sót người chơi trước đó.</summary>
    public bool EnableAiMode { get; set; }

    /// <summary>Chế độ chơi: classic, accuracy, no_backspace, sudden_death, ai_practice.</summary>
    public string GameMode { get; set; } = Constants.DefaultGameMode;

    /// <summary>Cấp độ bot AI luyện tập: easy, medium, hard, nightmare.</summary>
    public string AiPracticeDifficulty { get; set; } = Constants.DefaultAiPracticeDifficulty;

    /// <summary>
    /// Văn bản tùy chỉnh cho phòng, lấy cảm hứng từ custom/quote mode.
    /// Nếu rỗng thì server chọn passage từ database như bình thường.
    /// </summary>
    public string CustomPassageText { get; set; } = string.Empty;
}
