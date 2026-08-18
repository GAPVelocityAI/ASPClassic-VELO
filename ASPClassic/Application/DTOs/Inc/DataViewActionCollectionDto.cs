using ASPClassic.Domain.Entities.Data;
namespace ASPClassic.Application.DTOs.Inc;

/// <summary>Port of <c>Inc_Crudeconstants.InitDataViewActions</c> result — collection of action metadata for a DataView.</summary>
public class DataViewActionCollectionDto
{
    public int ViewID { get; set; }
    public List<DataViewInlineActionButtonDto> Actions { get; set; } = new();
}
