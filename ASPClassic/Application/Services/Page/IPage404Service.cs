namespace ASPClassic.Application.Services.Page;

public interface IPage404Service
{
    Task LoadPage404Async(string queryString,
        string https,
        string serverPort,
        string serverName,
        CancellationToken ct = default);
}
