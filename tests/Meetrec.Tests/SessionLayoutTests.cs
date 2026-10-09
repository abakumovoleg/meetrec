using Meetrec.Core.Transcript;

namespace Meetrec.Tests;

public class SessionLayoutTests
{
    [Theory]
    [InlineData("olabakumov", "mz-olabakumov.wav")]
    [InlineData("ivan.petrov_2", "mz-ivan.petrov_2.wav")]
    [InlineData("../evil/имя", "mz-.._evil____.wav")]
    public void ParticipantFile_KeepsSafeCharactersOfLogin(string login, string expected)
    {
        // Act
        var file = SessionLayout.ParticipantFile(login);

        // Assert
        Assert.Equal(expected, file);
    }

    [Fact]
    public void Meetzone_SpeakerIdIsLoginWithPrefix()
    {
        // Act
        var id = SpeakerId.Meetzone("olabakumov");

        // Assert
        Assert.Equal("mz:olabakumov", id.Value);
    }
}
