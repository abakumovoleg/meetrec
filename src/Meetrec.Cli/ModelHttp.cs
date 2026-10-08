namespace Meetrec.Cli;

internal static class ModelHttp
{
    public static HttpClient Create()
    {
        var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("meetrec/" + typeof(ModelHttp).Assembly.GetName().Version);
        return http;
    }
}
