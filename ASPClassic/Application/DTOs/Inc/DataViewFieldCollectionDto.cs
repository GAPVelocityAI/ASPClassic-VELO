using ASPClassic.Domain.Entities.Data;
namespace ASPClassic.Application.DTOs.Inc;

/// <summary>Port of <c>Inc_Crudeconstants.InitDataViewFields</c> result — collection of field metadata for a DataView.</summary>
public class DataViewFieldCollectionDto
{
    public int ViewID { get; set; }
    public List<DataViewFieldJsColumnDto> Fields { get; set; } = new();
}
