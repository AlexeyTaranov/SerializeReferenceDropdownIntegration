using System;
using JetBrains.Application.CommandProcessing;
using JetBrains.Application.Parts;
using JetBrains.DocumentManagers;
using JetBrains.DocumentModel;
using JetBrains.DocumentModel.Storage;
using JetBrains.Lifetimes;
using JetBrains.ProjectModel;
using JetBrains.Util;

namespace ReSharperPlugin.SerializeReferenceDropdownIntegration.Unity.AssetsDatabase;

[SolutionComponent(Instantiation.DemandAnyThreadSafe)]
public sealed class UnityAssetReferenceRiderDocumentWriter : UnityAssetReferenceDocumentWriter
{
    public UnityAssetReferenceRiderDocumentWriter(DocumentManager documentManager,
        IDocumentStorageHelpers documentStorageHelpers, ICommandProcessor commandProcessor)
        : base(new RiderDocumentSession(documentManager, documentStorageHelpers, commandProcessor))
    {
    }

    private sealed class RiderDocumentSession : IUnityAssetReferenceDocumentSession
    {
        private readonly DocumentManager documentManager;
        private readonly IDocumentStorageHelpers documentStorageHelpers;
        private readonly ICommandProcessor commandProcessor;

        public RiderDocumentSession(DocumentManager documentManager,
            IDocumentStorageHelpers documentStorageHelpers, ICommandProcessor commandProcessor)
        {
            this.documentManager = documentManager;
            this.documentStorageHelpers = documentStorageHelpers;
            this.commandProcessor = commandProcessor;
        }

        public void ExecuteUndoableCommand(string commandName, Action action)
        {
            Lifetime.Using(commandLifetime =>
            {
                if (commandProcessor.IsExecutingCommand.Value)
                {
                    commandProcessor.ProlongCurrentCommand(commandLifetime, commandName);
                }
                else
                {
                    commandProcessor.BeginBatchTextChange(commandLifetime, commandName);
                }

                action();
            });
        }

        public IUnityAssetReferenceDocument OpenDocument(string filePath)
        {
            var path = VirtualFileSystemPath.Parse(
                filePath,
                InteractionContext.SolutionContext,
                FileSystemPathInternStrategy.INTERN);
            var document = documentManager.GetOrCreateDocument(path);

            return new RiderDocument(documentManager, documentStorageHelpers, document);
        }
    }

    private sealed class RiderDocument : IUnityAssetReferenceDocument
    {
        private readonly DocumentManager documentManager;
        private readonly IDocumentStorageHelpers documentStorageHelpers;
        private readonly IDocument document;

        public RiderDocument(DocumentManager documentManager,
            IDocumentStorageHelpers documentStorageHelpers, IDocument document)
        {
            this.documentManager = documentManager;
            this.documentStorageHelpers = documentStorageHelpers;
            this.document = document;
        }

        public string Text => document.GetText();

        public void ReplaceText(string text)
        {
            documentManager.EnsureWritable(document, cookie =>
            {
                if (cookie.EnsureWritableResult != EnsureWritableResult.SUCCESS)
                {
                    throw new InvalidOperationException(cookie.Message);
                }

                document.ReplaceText(new TextRange(0, document.GetTextLength()), text);
            });
        }

        public void Save()
        {
            documentStorageHelpers.SaveDocumentAfterModification(document, true);
            documentStorageHelpers.SaveDocument(document);
            documentStorageHelpers.CommitFileSystemTrackerChanges(document);
        }
    }
}
