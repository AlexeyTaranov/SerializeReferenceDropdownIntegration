namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename;

public enum MovedFromRefactoringBehavior
{
    Add,
    DontAdd,
    AddAndRemember,
    DontAddAndRemember,
}

public enum MovedFromRefactoringSettings
{
    ShowPopup,
    AlwaysAdd,
    NeverAdd,
}

public class MovedFromRenameModel
{
    //TODO Setup settings?
    private static MovedFromRefactoringSettings ShowPopupSettings;
    public MovedFromRefactoringBehavior MovedFromRefactoringBehavior { get; private set; }

    public MovedFromRenameModel()
    {
        MovedFromRefactoringBehavior = ShowPopupSettings switch
        {
            MovedFromRefactoringSettings.AlwaysAdd => MovedFromRefactoringBehavior.AddAndRemember,
            MovedFromRefactoringSettings.NeverAdd => MovedFromRefactoringBehavior.DontAddAndRemember,
            _ => MovedFromRefactoringBehavior.Add
        };
    }

    public void Commit(bool shouldAddFormerlySerializedAs, bool rememberSelectedOptionAndNeverShowPopup)
    {
        MovedFromRefactoringBehavior = shouldAddFormerlySerializedAs
            ? rememberSelectedOptionAndNeverShowPopup
                ? MovedFromRefactoringBehavior.AddAndRemember
                : MovedFromRefactoringBehavior.Add
            : rememberSelectedOptionAndNeverShowPopup
                ? MovedFromRefactoringBehavior.DontAddAndRemember
                : MovedFromRefactoringBehavior.DontAdd;

        ShowPopupSettings = MovedFromRefactoringBehavior switch
        {
            MovedFromRefactoringBehavior.AddAndRemember => MovedFromRefactoringSettings.AlwaysAdd,
            MovedFromRefactoringBehavior.DontAddAndRemember => MovedFromRefactoringSettings.NeverAdd,
            _ => MovedFromRefactoringSettings.ShowPopup
        };
    }
}