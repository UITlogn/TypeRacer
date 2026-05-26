using TypeRacer.Shared;

namespace TypeRacer.Tests.Shared;

public class ConstantsTests
{
    [Theory]
    [InlineData("classic", Constants.GameModeClassic)]
    [InlineData(" accuracy ", Constants.GameModeAccuracy)]
    [InlineData("NO_BACKSPACE", Constants.GameModeNoBackspace)]
    [InlineData("Sudden_Death", Constants.GameModeSuddenDeath)]
    [InlineData("ai_practice", Constants.GameModeAiPractice)]
    [InlineData("unknown", Constants.GameModeClassic)]
    [InlineData(null, Constants.GameModeClassic)]
    public void NormalizeGameMode_OnlyAllowsKnownModes(string? raw, string expected)
    {
        Assert.Equal(expected, Constants.NormalizeGameMode(raw));
    }

    [Theory]
    [InlineData("easy", Constants.AiPracticeEasy, Constants.AiPracticeEasyRpm)]
    [InlineData(" medium ", Constants.AiPracticeMedium, Constants.AiPracticeMediumRpm)]
    [InlineData("HARD", Constants.AiPracticeHard, Constants.AiPracticeHardRpm)]
    [InlineData("nightmare", Constants.AiPracticeNightmare, Constants.AiPracticeNightmareRpm)]
    [InlineData("invalid", Constants.AiPracticeEasy, Constants.AiPracticeEasyRpm)]
    [InlineData(null, Constants.AiPracticeEasy, Constants.AiPracticeEasyRpm)]
    public void AiDifficulty_NormalizesAndMapsToRpm(string? raw, string expectedDifficulty, int expectedRpm)
    {
        Assert.Equal(expectedDifficulty, Constants.NormalizeAiPracticeDifficulty(raw));
        Assert.Equal(expectedRpm, Constants.GetAiPracticeTargetRpm(raw));
    }

    [Fact]
    public void AiPracticeDifficulty_RpmAndAccuracyIncreaseWithDifficulty()
    {
        Assert.True(Constants.AiPracticeEasyRpm < Constants.AiPracticeMediumRpm);
        Assert.True(Constants.AiPracticeMediumRpm < Constants.AiPracticeHardRpm);
        Assert.True(Constants.AiPracticeHardRpm < Constants.AiPracticeNightmareRpm);

        Assert.True(Constants.GetAiPracticeAccuracy(Constants.AiPracticeEasy) <
                    Constants.GetAiPracticeAccuracy(Constants.AiPracticeMedium));
        Assert.True(Constants.GetAiPracticeAccuracy(Constants.AiPracticeMedium) <
                    Constants.GetAiPracticeAccuracy(Constants.AiPracticeHard));
        Assert.True(Constants.GetAiPracticeAccuracy(Constants.AiPracticeHard) <
                    Constants.GetAiPracticeAccuracy(Constants.AiPracticeNightmare));
    }

    [Fact]
    public void RaceDurationBounds_AreSafeForDemoRooms()
    {
        Assert.Equal(30, Constants.MinRaceDurationSeconds);
        Assert.Equal(20 * 60, Constants.MaxRaceDurationSeconds);
        Assert.InRange(Constants.DefaultRaceDurationSeconds, Constants.MinRaceDurationSeconds, Constants.MaxRaceDurationSeconds);
    }
}
