#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Domain.Entities.Data;

/// <summary>Port of <c>ASPClassic:portal.DataViewFieldTypes</c>. Deterministically generated from the plan's schema.</summary>
public class DataViewFieldTypes
{
    public string TypeValue { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string TypeWrappers { get; set; } = string.Empty;
    public string TypeIdentifier { get; set; } = string.Empty;
    public string TypeGroup { get; set; } = string.Empty;
}
