using ASPClassic.Application.DTOs.Inc;
using ASPClassic.Application.DTOs.Core;

namespace ASPClassic.Application.Services.Inc;

/// <summary>Port of <c>Inc_Functions</c> (inc_functions.asp) — utility functions for formatting, localization, navigation tree building, and lookup rendering.</summary>
public interface IIncFunctionsService
{
    Task<string?> GetWordAsync(IncFunctionsStateDto state, string strKey, CancellationToken ct = default);
    Task DisplayLookupSelectionAsync(string dBTableName, string dBValueCol, string dBIdentCol, string valSelectedIdent, string strOrderBy, CancellationToken ct = default);
    Task<string?> GetPageTitleAsync(IncFunctionsStateDto state, CancellationToken ct = default);
    Task<string?> FormatDateForDBAsync(string dt, CancellationToken ct = default);
    Task<string?> isIsoDateAsync(string s_input, CancellationToken ct = default);
    Task<string?> AutoFormatLabelsAsync(string colName, CancellationToken ct = default);
    Task<IncFunctionsStateDto> LoadIncFunctionsAsync(IncFunctionsStateDto state, CancellationToken ct = default);
    Task<string> FormatValueForJsonAsync(string value);
    Task<List<NavigationDto>> GetNavigationRecursiveAsync(int parentNavId);
    List<LookupSelectionItemDto> GetLookupSelectionItems();
    string HTMLFormControl(string pInput);
    string HTMLDisplay(string pInput);
    string Querystring(string pInput);
    string SQL(string pInput);
    string JSON(string str);
}
