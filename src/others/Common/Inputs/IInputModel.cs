using Common.Inputs.BaseClasses;

namespace Common.Inputs;

public interface IInputModel
{
    string SearchText { get; set; }
}

public class InputModel : BaseInputModel
{
    public string? SearchText { get; set; }
}