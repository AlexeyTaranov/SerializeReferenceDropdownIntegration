using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Collections.Viewable;
using JetBrains.DataFlow;
using JetBrains.IDE.UI.Extensions;
using JetBrains.Lifetimes;
using JetBrains.ReSharper.Feature.Services.Refactorings;
using JetBrains.Rider.Model.UIAutomation;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

public enum ModifyYamlShowBehaviour
{
    ShowAlways,
    DontShow
}

public class ModifyUnityAssetRefactoringPage : SingleBeRefactoringPage, IDisposable
{
    public static ModifyYamlShowBehaviour ShowBehaviour { get; private set; }


    private readonly BeGrid myContent;
    private readonly IProperty<bool> hidePageOnThisSession;

    private readonly Action fetchReferencesCount;

    private CancellationTokenSource closePageCancellationToken;

    public ModifyUnityAssetRefactoringPage(Lifetime lifetime, ModifyUnityAssetImplementation implementation) : base(lifetime)
    {
        closePageCancellationToken = new CancellationTokenSource();
        myContent = BeControls.BeLabel("This feature without UNDO! Keep modifications in VCS!").InAutoGrid();

        myContent.AddElement(new BeSpacer());

        var filesCountLabel = BeControls.BeLabel("?");
        var referencesCount = new Property<int>("References count in assets folder", -1);
        var fetchingFilesCount = new Property<bool>("Fetching files count", false);
        referencesCount.PropertyChanged += (sender, args) => filesCountLabel.SetText(GetFilesCountText());
        fetchingFilesCount.PropertyChanged += (sender, args) => filesCountLabel.SetText(GetFilesCountText());
        myContent.AddElement(filesCountLabel);

        var fetchFilesCountVisibility = new ViewableProperty<bool>(true);
        var modifyFilesVisibility = new ViewableProperty<bool>(false);
        var fetchFilesCountButton = BeControls.GetButton("Fetch references count in Assets folder", lifetime,
            () => fetchReferencesCount.Invoke());
        fetchFilesCountButton.EnableWhen(lifetime, fetchFilesCountVisibility);
        fetchReferencesCount = () => FetchCountAsync();

        myContent.AddElement(fetchFilesCountButton);
        myContent.AddElement(new BeSpacer());
        
        var modifiedFilesLabel = BeControls.BeLabel("Modified files: PENDING");
        var modifiedFilesLabelVisibility = new ViewableProperty<bool>(false);
        modifiedFilesLabel.EnableWhen(lifetime, modifiedFilesLabelVisibility);
        var modifyAllFilesButton = BeControls.GetButton("Modify all files", lifetime, () => ModifyFilesAsync());
        modifyAllFilesButton.EnableWhen(lifetime, modifyFilesVisibility);
        myContent.AddElement(modifyAllFilesButton);
        myContent.AddElement(modifiedFilesLabel);

        myContent.AddElement(new BeSpacer());


        hidePageOnThisSession = new Property<bool>("Hide modify file on this session",
            ShowBehaviour == ModifyYamlShowBehaviour.DontShow);
        myContent.AddElement(hidePageOnThisSession.GetBeCheckBox(lifetime, "Hide this window"));

        string GetFilesCountText()
        {
            var mainText = "Reference count in assets folder: ";
            if (fetchingFilesCount.Value == true)
            {
                return mainText + "PENDING";
            }

            var filesCountText = referencesCount.Value > -1 ? referencesCount.Value.ToString() : "NEED COUNT";
            return $" {filesCountText}";
        }

        async Task FetchCountAsync()
        {
            fetchingFilesCount.Value = true;
            fetchFilesCountVisibility.Value = false;
            
            var result = await Task.Run(() =>
                implementation.FetchSerializeReferenceCountInAssetsFolderAsync(closePageCancellationToken.Token));
            
            referencesCount.Value = result;
            fetchingFilesCount.Value = false;
            modifyFilesVisibility.Value = result > 0;
        }

        async Task ModifyFilesAsync()
        {
            ContinueEnabled.Value = false;
            fetchFilesCountVisibility.Value = false;
            modifyFilesVisibility.Value = false;
            modifiedFilesLabelVisibility.Value = true;
            
            await Task.Run(implementation.ModifyAllFilesAsync);
            
            ContinueEnabled.Value = true;
            modifiedFilesLabel.SetText("Modified files: COMPLETE");
        }
    }

    public override BeControl GetPageContent() => myContent;

    public override void Commit()
    {
        ShowBehaviour = hidePageOnThisSession.Value
            ? ModifyYamlShowBehaviour.DontShow
            : ModifyYamlShowBehaviour.ShowAlways;

        closePageCancellationToken.Cancel();
        closePageCancellationToken.Dispose();
        closePageCancellationToken = null;
    }


    public override bool DoNotShow => ShowBehaviour == ModifyYamlShowBehaviour.DontShow;

    public override string Title => "Modify YAML content in Assets directory";

    public override string Description => "Modify assets content in Unity Project";

    public void Dispose()
    {
        hidePageOnThisSession?.Dispose();
        closePageCancellationToken?.Dispose();
    }
}