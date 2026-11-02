namespace Common.Inputs.BaseClasses;

public class BasePagerInputModel : BaseInputModel, IPagerInputModel
{
    protected BasePagerInputModel()
    {
        PageSize = int.MaxValue;
        PageNumber = 1;
    }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    private string _pSortColumn = string.Empty;
    public string SortColumn
    {
        get => !string.IsNullOrEmpty(_pSortColumn) ? _pSortColumn : "CreatedTime";
        set => _pSortColumn = value;
    }
    private bool? _pSortDescending = true;
    public bool SortDescending
    {
        get => _pSortDescending ?? true;
        set => _pSortDescending = value;
    }

    public virtual string ParamString()
    {
        var format = "pageNumber:{0}-pageSize:{1}-sortColumn:{2}-sortDescending:{3}";
        return string.Format(format, PageNumber, PageSize, SortColumn, SortDescending);
    }
}