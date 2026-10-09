using Meetrec.Capture.Mac;
using Meetrec.Core.Capture;

namespace Meetrec.Tests;

public class HelperOutputTests
{
    private const string Hello = """{"t":"hello","protocol":1,"version":"0.1.0"}""";

    [Fact]
    public void Accept_GreetingThenMessage_ReturnsMessage()
    {
        // Arrange
        var output = new HelperOutput();

        // Act
        var greeting = output.Accept(Hello);
        var started = output.Accept("""{"t":"started","offsetMs":5}""");

        // Assert
        Assert.Null(greeting);
        Assert.Equal(new HelperMessage.Started(5), started);
    }

    [Fact]
    public void Accept_OtherProtocol_ThrowsWithBothVersions()
    {
        // Arrange
        var output = new HelperOutput();

        // Act
        var act = () => output.Accept("""{"t":"hello","protocol":2,"version":"0.9.0"}""");

        // Assert
        var error = Assert.Throws<CaptureException>(act);
        Assert.Contains("протоколе 2", error.Message);
        Assert.Contains("0.9.0", error.Message);
    }

    [Fact]
    public void Accept_MessageBeforeGreeting_ThrowsAsOldHelper()
    {
        // Arrange
        var output = new HelperOutput();

        // Act
        var act = () => output.Accept("""{"t":"started","offsetMs":0}""");

        // Assert
        Assert.Contains("не представился", Assert.Throws<CaptureException>(act).Message);
    }

    [Fact]
    public void Explain_AfterUnrecognizedLines_AppendsThem()
    {
        // Arrange
        var output = new HelperOutput();
        output.Accept("warning: something odd");
        output.Accept(Hello);
        output.Accept("""{"t":"future","x":1}""");

        // Act
        var text = output.Explain("Хелпер записи завершился с кодом 1.");

        // Assert
        Assert.Equal("Хелпер записи завершился с кодом 1.\nНепонятный вывод хелпера:\nwarning: something odd\n{\"t\":\"future\",\"x\":1}", text);
    }

    [Fact]
    public void Explain_NothingUnrecognized_ReturnsReason()
    {
        // Arrange
        var output = new HelperOutput();
        output.Accept(Hello);

        // Act
        var text = output.Explain("причина");

        // Assert
        Assert.Equal("причина", text);
    }
}
