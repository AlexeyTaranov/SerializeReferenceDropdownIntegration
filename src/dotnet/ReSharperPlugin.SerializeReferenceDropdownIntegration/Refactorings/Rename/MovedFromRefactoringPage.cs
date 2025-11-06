using System.Collections.Generic;
using JetBrains.DataFlow;
using JetBrains.IDE.UI.Extensions;
using JetBrains.Lifetimes;
using JetBrains.ReSharper.Feature.Services.Refactorings;
using JetBrains.Rider.Model.UIAutomation;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename;

public class MovedFromRefactoringPage : SingleBeRefactoringPage
{
    private readonly MovedFromRenameModel myModel;
    private readonly BeGrid myContent;
    private readonly IProperty<bool> myShouldAddFormerlySerializedAs;
    private readonly IProperty<bool> myRememberSelectedOptionAndNeverShowPopup;

    // TODO Replace strings with resourceManager?
    public MovedFromRefactoringPage(Lifetime lifetime,
        MovedFromRenameModel model, string oldName) : base(lifetime)
    {
        myModel = model;

        myShouldAddFormerlySerializedAs = new Property<bool>("Should add attribute action",
            model.MovedFromRefactoringBehavior is MovedFromRefactoringBehavior.Add
                or MovedFromRefactoringBehavior.AddAndRemember);


        myContent = myShouldAddFormerlySerializedAs.GetBeRadioGroup(lifetime,
            $"Add attribute MovedFrom to class: {oldName}",
            new List<bool> { true, false },
            present: (settings, properties) => settings ? "Add" : "Don't Add",
            horizontal: false
        ).InAutoGrid();


        myRememberSelectedOptionAndNeverShowPopup = new Property<bool>("Save settings for this session",
            model.MovedFromRefactoringBehavior
                is MovedFromRefactoringBehavior.AddAndRemember
                or MovedFromRefactoringBehavior.DontAddAndRemember);
        myContent.AddElement(new BeSpacer());
        myContent.AddElement(myRememberSelectedOptionAndNeverShowPopup.GetBeCheckBox(lifetime, "Remember settings"));
    }

    public override BeControl GetPageContent() => myContent;

    public override void Commit()
    {
        var shouldAdd = myShouldAddFormerlySerializedAs.Value;
        var rememberSelectedOption = myRememberSelectedOptionAndNeverShowPopup?.Value ?? false;

        myModel.Commit(shouldAdd, rememberSelectedOption);
    }

    public override string Title => "Rename with MovedFrom unity Attribute";
    public override string Description => "Renaming a class can break serialize references to this class";
}