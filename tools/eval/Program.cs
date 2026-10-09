using Meetrec.Core;
using Meetrec.Eval;

const string Usage = """
    meetrec-eval — стенд замеров meetrec

      run <cases.json> <out> [--options <recognition.json>]   прогнать конвейер по случаям, вывод в <out>/<случай>/
      compare <baseline> <out>                                  сравнить md/json/srt побайтно, код 1 при расхождении
      recognize <wav> <words.json> [--options …] [--lang ru] [--model large-v3-turbo]
                                                                только распознавание, слова с таймкодами в json
      timing <fixtures> [--options …] [--model …]              точность таймкодов на синтетике (say -v Milena)
      meetzone <probe.json> <папка>                             сессия участников из записи отладочного расширения Meetzone
      meetzone-replay <probe.json> [--port 47823]               проиграть запись в meetrec record --meetzone, как расширение
      options                                                   параметры распознавания по умолчанию в json
    """;

try
{
    var parsed = Args.Parse(args);
    return parsed.Command switch
    {
        "run" => await RunCommand.ExecuteAsync(parsed),
        "compare" => CompareCommand.Execute(parsed),
        "recognize" => await RecognizeCommand.ExecuteAsync(parsed),
        "timing" => await TimingCommand.ExecuteAsync(parsed),
        "meetzone" => MeetzoneCommand.Execute(parsed),
        "meetzone-replay" => await MeetzoneReplayCommand.ExecuteAsync(parsed),
        "options" => OptionsFile.Print(),
        _ => throw new MeetrecException(Usage),
    };
}
catch (MeetrecException e)
{
    Console.Error.WriteLine(e.Message);
    return 1;
}
