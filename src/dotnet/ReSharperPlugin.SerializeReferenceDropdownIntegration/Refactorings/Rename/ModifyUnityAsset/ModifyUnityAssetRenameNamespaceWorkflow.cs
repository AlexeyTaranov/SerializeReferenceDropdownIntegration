using JetBrains.Application.DataContext;
using JetBrains.Application.Progress;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Feature.Services.Refactorings;
using JetBrains.ReSharper.Refactorings.RenameNamespace;
using JetBrains.Rider.Model.Refactorings;
using JetBrains.Rider.Model.UIAutomation;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Infrastructure;
using ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Refactorings.Rename.ModifyUnityAsset;

public class ModifyUnityAssetRenameNamespaceWorkflow : RenameNamespaceWorkflow
{
    private readonly UnityAssetReferenceScanner scanner;
    private readonly UnityAssetReferenceDocumentWriter documentWriter;
    private readonly PluginSessionSettings sessionSettings;
    private readonly PluginDiagnostics diagnostics;
    private string oldNamespace;
    private ModifyUnityAssetModel model;
    private bool basePageReturned;
    private bool yamlPageReturned;

    public ModifyUnityAssetRenameNamespaceWorkflow(ISolution solution, string actionId, string oldNamespace,
        UnityAssetReferenceScanner scanner, UnityAssetReferenceDocumentWriter documentWriter,
        PluginSessionSettings sessionSettings, PluginDiagnostics diagnostics)
        : base(solution, actionId)
    {
        this.oldNamespace = oldNamespace;
        this.scanner = scanner;
        this.documentWriter = documentWriter;
        this.sessionSettings = sessionSettings;
        this.diagnostics = diagnostics;
    }

    public override bool Initialize(IDataContext context)
    {
        var initialized = base.Initialize(context);
        diagnostics.Info($"Namespace YAML workflow initialized={initialized}, oldNamespace='{oldNamespace}'.");
        return initialized && !string.IsNullOrEmpty(oldNamespace);
    }

    public override IRefactoringPage FirstPendingRefactoringPage
    {
        get
        {
            var basePage = base.FirstPendingRefactoringPage;
            if (basePage != null && !basePageReturned)
            {
                basePageReturned = true;
                diagnostics.Info($"Namespace YAML returning base namespace page, basePage={basePage.GetType().FullName}, oldNamespace='{oldNamespace}', newName='{NamespaceModel?.NewName}'.");
                return basePage is SingleBeRefactoringPage singleBePage
                    ? new ChainedSingleBeRefactoringPage(WorkflowExecuterLifetime, singleBePage, CreateYamlPage,
                        message => diagnostics.Info($"Namespace YAML chained page: {message}; namespaceModelNewName='{NamespaceModel?.NewName}'."))
                    : basePage;
            }

            if (yamlPageReturned ||
                sessionSettings.ModifyYamlShowBehaviour == ModifyYamlShowBehaviour.DontShow ||
                string.IsNullOrEmpty(oldNamespace) ||
                string.IsNullOrEmpty(NamespaceModel?.NewName))
            {
                diagnostics.Info($"Namespace YAML page skipped, basePage={basePage?.GetType().FullName ?? "<null>"}, pageReturned={yamlPageReturned}, oldNamespace='{oldNamespace}', newName='{NamespaceModel?.NewName}'.");
                return basePage;
            }

            diagnostics.Info($"Namespace YAML pending page requested, basePage={basePage?.GetType().FullName ?? "<null>"}, oldNamespace='{oldNamespace}', newName='{NamespaceModel.NewName}'.");
            return CreateYamlPage();
        }
    }

    public override bool PostExecute(IProgressIndicator pi)
    {
        var result = base.PostExecute(pi);
        model ??= CreateModel();
        diagnostics.Info($"Namespace YAML PostExecute result={result}, modelCreated={model != null}, shouldApply={model?.ShouldApplyModifiedFiles == true}.");
        if (!result || model == null)
        {
            return result;
        }

        try
        {
            model.ModifyAllFilesAsync().GetAwaiter().GetResult();
        }
        catch (System.Exception exception)
        {
            model.LogModificationFailure(exception);
        }

        return result;
    }

    private ModifyUnityAssetModel CreateModel()
    {
        var newNamespace = BuildNewNamespace(oldNamespace, NamespaceModel.NewName);
        if (string.IsNullOrEmpty(newNamespace))
        {
            return null;
        }

        return ModifyUnityAssetModel.CreateNamespaceRenameModel(
            oldNamespace,
            newNamespace,
            scanner,
            documentWriter,
            diagnostics);
    }

    private IRefactoringPage CreateYamlPage()
    {
        if (yamlPageReturned)
        {
            return null;
        }

        yamlPageReturned = true;
        model = CreateModel();
        diagnostics.Info($"Namespace YAML page created, oldNamespace='{oldNamespace}', newName='{NamespaceModel.NewName}', modelCreated={model != null}.");
        return model == null
                ? null
                : new ModifyUnityAssetRefactoringPage(WorkflowExecuterLifetime, model, sessionSettings, diagnostics);
    }

    private static string BuildNewNamespace(string oldQualifiedNamespace, string newName)
    {
        if (string.IsNullOrEmpty(oldQualifiedNamespace) || string.IsNullOrEmpty(newName))
        {
            return null;
        }

        if (newName.Contains("."))
        {
            return newName;
        }

        var lastDot = oldQualifiedNamespace.LastIndexOf('.');
        return lastDot < 0
            ? newName
            : $"{oldQualifiedNamespace.Substring(0, lastDot + 1)}{newName}";
    }

    private sealed class ChainedSingleBeRefactoringPage : SingleBeRefactoringPage
    {
        private readonly SingleBeRefactoringPage innerPage;
        private readonly System.Func<IRefactoringPage> createNextPage;
        private readonly System.Action<string> log;

        public ChainedSingleBeRefactoringPage(Lifetime lifetime, SingleBeRefactoringPage innerPage,
            System.Func<IRefactoringPage> createNextPage, System.Action<string> log)
            : base(lifetime)
        {
            this.innerPage = innerPage;
            this.createNextPage = createNextPage;
            this.log = log;
        }

        public override string Title => innerPage.Title;
        public override string Description => innerPage.Description;
        public override string PageDescription => innerPage.PageDescription;
        public override bool DoNotShow => innerPage.DoNotShow;

        public override bool Initialize(IProgressIndicator pi)
        {
            return innerPage.Initialize(pi);
        }

        public override bool RefreshContents(IProgressIndicator pi)
        {
            return innerPage.RefreshContents(pi);
        }

        public override BeControl GetPageContent()
        {
            return innerPage.GetPageContent();
        }

        public override AbstractPage GetModel()
        {
            return innerPage.GetModel();
        }

        public override IRefactoringPage Commit(IProgressIndicator pi)
        {
            log("committing base namespace page");
            var nextPage = innerPage.Commit(pi);
            log($"base namespace page committed, nextPage={nextPage?.GetType().FullName ?? "<null>"}");
            return nextPage ?? createNextPage();
        }
    }

}
