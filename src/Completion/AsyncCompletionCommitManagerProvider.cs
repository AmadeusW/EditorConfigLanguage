using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace EditorConfig
{
    [Export(typeof(IAsyncCompletionCommitManagerProvider))]
    [Name(nameof(Constants.FeatureName))]
    [ContentType(Constants.LanguageName)]
    class AsyncCompletionCommitManagerProvider : IAsyncCompletionCommitManagerProvider
    {
        IDictionary<ITextView, AsyncCompletionCommitManager> cache = new Dictionary<ITextView, AsyncCompletionCommitManager>();

        [Import]
        ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        IAsyncCompletionCommitManager IAsyncCompletionCommitManagerProvider.GetOrCreate(ITextView textView)
        {
            if (cache.TryGetValue(textView, out var commitManager))
                return commitManager;

            var manager = new AsyncCompletionCommitManager(textView, NavigatorService);
            textView.Closed += (o, e) =>
            {
                cache.Remove(textView);
            };
            cache.Add(textView, manager);
            return manager;
        }
    }

}
