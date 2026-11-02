using Common.Inputs.BaseClasses;

namespace Common.Inputs;

public interface IPagerInputModel : IInputModel
{
    string ParamString();
    int PageNumber { get; set; }
    int PageSize { get; set; }
    string SortColumn { get; set; }
    bool SortDescending { get; set; }
}

public class PagerInputModel : BasePagerInputModel
{
    public static PagerInputModel Create(int pageNumber, int pageSize, string sortColumn, string searchText, bool sortDescending = true)
    {
        if (pageNumber == 0)
        {
            pageNumber = 1;
        }

        if (pageSize == 0)
        {
            pageSize = 10;
        }

        if (string.IsNullOrEmpty(sortColumn))
        {
            sortColumn = "CreatedTime";
        }

        return new PagerInputModel
        {
            PageNumber = pageNumber,
            PageSize = pageSize,
            SortColumn = sortColumn,
            SearchText = searchText,
            SortDescending = sortDescending
        };
    }
}