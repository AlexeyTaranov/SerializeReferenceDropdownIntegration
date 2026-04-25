using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.MovedFrom;

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
    private readonly PluginSessionSettings sessionSettings;
    public MovedFromRefactoringBehavior MovedFromRefactoringBehavior { get; private set; }

    public MovedFromRenameModel(PluginSessionSettings sessionSettings)
    {
        this.sessionSettings = sessionSettings;
        MovedFromRefactoringBehavior = sessionSettings.MovedFromRefactoringSettings switch
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

        sessionSettings.MovedFromRefactoringSettings = MovedFromRefactoringBehavior switch
        {
            MovedFromRefactoringBehavior.AddAndRemember => MovedFromRefactoringSettings.AlwaysAdd,
            MovedFromRefactoringBehavior.DontAddAndRemember => MovedFromRefactoringSettings.NeverAdd,
            _ => MovedFromRefactoringSettings.ShowPopup
        };
    }
}
