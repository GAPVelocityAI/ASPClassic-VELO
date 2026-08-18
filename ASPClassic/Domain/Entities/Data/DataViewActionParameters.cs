#nullable disable
using System;
using System.Collections.Generic;

namespace ASPClassic.Domain.Entities.Data;

/// <summary>Port of <c>ASPClassic:portal.DataViewActionParameters table (reference)</c>. Deterministically generated from the plan's schema.</summary>
public class DataViewActionParameters
{
    public int ActionParameterId { get; set; }
    public int ActionID { get; set; }
    public string ParamSystemName { get; set; } = string.Empty;
    public string ParamLabel { get; set; } = string.Empty;
    public int ParamOrder { get; set; }
    public bool ParamIsRequired { get; set; }
    public string ParamDefaultValue { get; set; } = string.Empty;
    public string ParamTooltip { get; set; } = string.Empty;
    public string ParamDescription { get; set; } = string.Empty;
    public int ParamDataType { get; set; }
    public string ParamLinkedTable { get; set; } = string.Empty;
    public string ParamLinkedTableTitleField { get; set; } = string.Empty;
    public string ParamLinkedTableValueField { get; set; } = string.Empty;
    public string ParamLinkedTableGroupField { get; set; } = string.Empty;
    public string ParamLinkedTableAddition { get; set; } = string.Empty;

    public virtual ASPClassic.Domain.Entities.Data.DataViewAction Action { get; set; }
}
