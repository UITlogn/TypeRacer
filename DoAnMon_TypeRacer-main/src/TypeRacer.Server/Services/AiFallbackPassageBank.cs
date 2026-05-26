namespace TypeRacer.Server.Services;

public static class AiFallbackPassageBank
{
    private static readonly string[] Vietnamese =
    {
        "Buổi sáng trong trẻo, tôi mở cửa sổ, đọc trước một câu ngắn rồi gõ từng cụm thật đều tay.",
        "Khi gặp dấu phẩy hoặc dấu chấm, hãy chậm lại một nhịp để giữ accuracy ổn định.",
        "Một người luyện gõ tốt luôn đặt độ chính xác lên trước tốc độ trong vài phút đầu.",
        "Nhịp thở bình tĩnh giúp cổ tay bớt căng và giảm các lỗi lặp lại ở đoạn cuối.",
        "Tôi tập nhìn trước ba từ, sau đó để các ngón tay di chuyển nhẹ và chắc.",
        "Nếu sai một ký tự, hãy ghi nhớ vị trí lệch nhịp rồi tiếp tục hoàn thành câu.",
        "Đoạn văn ngắn này giúp bạn kiểm soát khoảng trắng, dấu câu và các cụm nhiều dấu.",
        "Mỗi lần luyện chỉ cần tốt hơn một chút, miễn là nhịp gõ vẫn sạch và bền.",
        "Trước khi tăng tốc, hãy chạy một lượt chậm để kiểm tra các ký tự dễ nhầm.",
        "Một bài gõ ổn định thường bắt đầu bằng mắt nhìn xa hơn tay một vài từ.",
        "Hãy giữ vai thả lỏng, cổ tay trung lập và tập trung vào câu đang hiện trước mắt.",
        "Khi đoạn văn có nhiều dấu tiếng Việt, tốc độ vừa phải sẽ giúp bạn ít sửa hơn.",
        "Tôi luyện từng cụm nhỏ, nghỉ một nhịp ngắn, rồi tiếp tục bằng tốc độ ổn định.",
        "Một vòng luyện hiệu quả là vòng mà số lỗi lặp lại giảm rõ so với lần trước.",
        "Đừng cố bù tốc sau khi sai; quay lại nhịp chính sẽ giữ kết quả tốt hơn.",
        "Câu này dùng để luyện nhịp đều, khoảng trắng chuẩn và khả năng đọc trước.",
        "Tốc độ bền không đến từ gồng tay, mà từ nhiều lượt gõ sạch và đều.",
        "Khi gặp từ dài, hãy chia nó thành âm tiết nhỏ để ngón tay không bị rối.",
        "Một phút luyện chậm nhưng đúng có giá trị hơn nhiều lượt gõ nhanh đầy lỗi.",
        "Hãy kết thúc đoạn cuối bằng cùng sự bình tĩnh như khi bạn bắt đầu bài.",
        "Tôi đặt mục tiêu ít lỗi hơn trước, rồi mới nâng WPM ở lượt luyện tiếp theo.",
        "Nếu đoạn này quá dễ, hãy tăng nhẹ nhịp nhưng không để accuracy tụt xuống.",
        "Mắt đọc cụm tiếp theo, tay giữ nhịp hiện tại, tâm trí tránh nhìn lại lỗi cũ.",
        "Một chuỗi phím sạch sẽ giúp bạn tự tin hơn khi bước vào trận đua kế tiếp.",
        "Bạn có thể luyện câu ngắn này ba lượt: chậm, vừa, rồi nhanh có kiểm soát.",
        "Dấu thanh và dấu câu cần được gõ rõ ràng, không vội vàng ở những từ khó.",
        "Tôi sửa thói quen gõ bằng cách lặp lại đúng lỗi cũ trong một câu mới.",
        "Khi tay bắt đầu vội, hãy thả lỏng ngón út và giảm nhịp trong vài ký tự.",
        "Một đoạn luyện tốt nên vừa đủ ngắn để tập trung và đủ dài để thấy nhịp.",
        "Hãy ưu tiên hoàn thành bài sạch, vì tốc độ sẽ tăng tự nhiên sau vài vòng.",
        "Sự ổn định đến từ việc gõ cùng một nhịp, không tăng giảm đột ngột giữa câu.",
        "Mỗi cụm từ trong bài này được thiết kế để bạn đọc trước và gõ chính xác.",
        "Nếu bạn hay sai ở đoạn cuối, hãy giữ nhịp chậm hơn từ giữa bài trở đi.",
        "Tôi không nhìn lại phím vừa gõ, mà tập trung vào từ kế tiếp trên màn hình.",
        "Luyện lỗi cũ trong văn cảnh mới giúp tay nhớ đúng vị trí thay vì học vẹt.",
        "Một vài ký tự khó không đáng sợ nếu bạn chủ động chậm lại trước khi gõ.",
        "Hãy thử gõ đoạn này không dùng backspace để rèn sự tập trung và nhịp đều.",
        "Trận đua tốt nhất là trận mà bạn kiểm soát được cả tốc độ lẫn độ chính xác.",
        "Khi câu có nhiều từ ngắn, đừng tăng tốc quá mạnh làm mất khoảng trắng chuẩn.",
        "Đọc trước, gõ đều, sửa ít: ba việc nhỏ này tạo ra một lượt luyện hiệu quả.",
        "Tôi luyện bằng cách chọn một lỗi chính, rồi đưa lỗi đó vào nhiều câu tự nhiên.",
        "Nếu phải sửa quá nhiều, hãy giảm nhịp và giữ tay nhẹ hơn ở lượt tiếp theo.",
        "Một đoạn văn rõ nghĩa sẽ giúp bạn luyện tốc độ mà vẫn hiểu nhịp câu.",
        "Khi gõ tiếng Việt, hãy để mắt dẫn đường qua dấu thanh và các âm tiết dài.",
        "Lỗi nhỏ lặp lại nhiều lần thường quan trọng hơn một lỗi lớn chỉ xuất hiện một lần.",
        "Hãy giữ hơi thở đều trong ba mươi giây cuối để tránh chuỗi lỗi dây chuyền.",
        "Tôi kiểm soát tốc độ bằng cách gõ chính xác từng cụm, không chạy theo đồng hồ.",
        "Một bài luyện ngắn có thể tạo cảm giác chắc tay nếu bạn lặp lại đúng trọng tâm.",
        "Nếu ký tự khó xuất hiện, hãy nhấn phím dứt khoát nhưng không căng cổ tay.",
        "Mục tiêu của đoạn này là giảm lỗi đã từng xảy ra, kể cả khi bạn đã sửa lại.",
        "Bạn nên gõ chậm lượt đầu, đánh dấu lỗi, rồi chạy lại với nhịp cao hơn một chút.",
        "Các cụm từ trong câu này giúp luyện dấu câu, khoảng trắng và nhịp chuyển tay.",
        "Tôi không cần gõ hoàn hảo ngay, chỉ cần ít lỗi lặp lại hơn mỗi lần luyện.",
        "Khi bị lệch nhịp, hãy thả chậm một cụm rồi trở lại tốc độ ban đầu.",
        "Đoạn văn này ngắn nhưng đủ để kiểm tra khả năng giữ accuracy trên chín mươi lăm phần trăm.",
        "Hãy tập trung vào tay yếu hơn, vì nhiều lỗi lặp lại thường đến từ một ngón cụ thể.",
        "Một câu luyện mới giúp bạn sửa lỗi thật mà không cảm thấy đang học thuộc bài cũ.",
        "Nếu đoạn có dấu hỏi hoặc dấu ngã, hãy đọc rõ âm trước khi đặt tay gõ.",
        "Bạn đang luyện khả năng phục hồi sau lỗi, không chỉ luyện tốc độ thuần túy.",
        "Khi nhìn thấy từ quen, vẫn giữ nhịp vừa phải để tránh sai ở ký tự cuối.",
        "Tôi dùng từng câu ngắn để xây lại sự tự tin sau một trận đua nhiều lỗi.",
        "Lỗi đã sửa vẫn là dữ liệu quan trọng, vì nó cho biết tay bạn từng lệch ở đâu.",
        "Đoạn luyện này ưu tiên sự sạch sẽ: ít sửa, ít vấp, và nhịp đều đến cuối.",
        "Hãy gõ như đang đọc thầm: câu nào rõ trong đầu thì tay sẽ ít sai hơn.",
        "Một lượt luyện tốt kết thúc bằng cảm giác nhẹ tay, không phải mỏi và căng.",
        "Đừng bỏ qua khoảng trắng, vì lỗi spacing thường kéo theo sai cả từ tiếp theo.",
        "Tôi tập nhận diện lỗi cũ bằng văn bản mới, để phản xạ sửa trở nên tự nhiên.",
        "Nếu accuracy thấp, hãy chọn nhịp chậm hơn và chỉ tăng tốc sau khi sạch lỗi.",
        "Bài luyện này giúp bạn giữ sự bình tĩnh khi gặp từ dài ở giữa câu.",
        "Hãy hoàn thành câu cuối bằng nhịp chắc, vì nhiều người thường mất tập trung ở đó."
    };

    private static readonly string[] English =
    {
        "Start with a calm rhythm, read three words ahead, and keep each keystroke deliberate.",
        "When punctuation appears, slow down slightly so the next phrase stays clean.",
        "Accuracy grows faster when you repeat old mistakes inside fresh sentences.",
        "A short focused drill can rebuild confidence after a race with too many errors.",
        "Keep your shoulders relaxed, your wrists neutral, and your eyes ahead of your hands.",
        "If one letter breaks your rhythm, notice the spot and continue without rushing.",
        "Clean spacing matters because one missed space can disturb the whole next word.",
        "Practice this sentence slowly once, then repeat it with a small increase in pace.",
        "The goal is fewer repeated mistakes, not a perfect run on the first attempt.",
        "A steady final line is often the difference between a good race and a messy one.",
        "Do not chase the timer after an error; return to your normal typing cadence.",
        "Read the phrase before typing it, then let your fingers move with less tension.",
        "A useful typing drill is short enough to repeat and long enough to expose rhythm.",
        "When a long word appears, divide it into smaller chunks before your hands arrive.",
        "One clean minute of typing can teach more than several rushed attempts.",
        "Protect accuracy first, then raise speed only after the repeated mistakes fade.",
        "A fresh sentence keeps practice honest because your hands cannot simply memorize it.",
        "If the same key causes trouble, slow down before it instead of repairing afterward.",
        "Good recovery means continuing with control, not bursting through the next words.",
        "Use this line to train punctuation, spacing, and steady phrase transitions.",
        "A lighter touch can reduce fatigue and make the final seconds more consistent.",
        "Focus on the next word instead of staring at the mistake you already corrected.",
        "Small improvements compound when every drill targets a real mistake from play.",
        "Keep the first ten seconds clean so the rest of the passage feels easier.",
        "Typing speed becomes durable when the rhythm stays even under pressure.",
        "Repeat the old trouble spots in new contexts until the correct motion feels normal.",
        "A controlled pace helps you avoid turning one typo into a chain of errors.",
        "The best practice text makes your weak keys appear naturally inside real sentences.",
        "Finish this passage with the same control you used at the beginning.",
        "Every corrected mistake still matters because it shows where your rhythm slipped."
    };

    public static List<string> Pick(
        string language,
        string focusArea,
        IReadOnlyList<string> mistakeCharacters,
        IReadOnlyList<string> mistakeWords,
        int count)
    {
        var normalizedLanguage = (language ?? "vi").Trim().ToLowerInvariant();
        var source = normalizedLanguage == "en" ? English : Vietnamese;
        var targets = mistakeCharacters
            .Concat(mistakeWords)
            .Select(NormalizeTarget)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = source
            .Select((text, index) => new
            {
                Text = text,
                Index = index,
                Score = Score(text, focusArea, targets),
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => StableOffset(x.Index, focusArea, targets))
            .Select(x => x.Text)
            .Take(Math.Clamp(count, 1, 12))
            .ToList();

        return selected.Count > 0 ? selected : source.Take(Math.Clamp(count, 1, 12)).ToList();
    }

    private static int Score(string text, string focusArea, IReadOnlyList<string> targets)
    {
        var score = 0;
        var lower = text.ToLowerInvariant();

        foreach (var target in targets)
        {
            if (target.Length == 1)
                score += lower.Count(ch => ch.ToString().Equals(target, StringComparison.OrdinalIgnoreCase));
            else if (lower.Contains(target.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
                score += 4;
        }

        if (focusArea.Contains("chính xác", StringComparison.OrdinalIgnoreCase) ||
            focusArea.Contains("accuracy", StringComparison.OrdinalIgnoreCase))
        {
            score += lower.Contains("accuracy", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
            score += lower.Contains("chính xác", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        }

        return score;
    }

    private static int StableOffset(int index, string focusArea, IReadOnlyList<string> targets)
    {
        var seed = focusArea.Length + targets.Sum(x => x.Length * 17);
        return Math.Abs((index * 37 + seed) % 101);
    }

    private static string NormalizeTarget(string value)
    {
        var raw = value.Trim();
        var openParen = raw.IndexOf("(", StringComparison.Ordinal);
        if (openParen > 0)
            raw = raw[..openParen].Trim();

        return raw is "space" or "\\n" or "\\r" or "\\t" ? string.Empty : raw;
    }
}
