using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion.Data;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace EditorConfig
{
    class AsyncCompletionCommitManager : IAsyncCompletionCommitManager
    {
        private ITextView textView;
        private ITextStructureNavigatorSelectorService navigatorService;
        private ImmutableArray<char> commitChars = new[] { ':', '=', ' ', ',' }.ToImmutableArray();

        public AsyncCompletionCommitManager(ITextView textView, ITextStructureNavigatorSelectorService navigatorService)
        {
            this.textView = textView;
            this.navigatorService = navigatorService;
        }

        IEnumerable<char> IAsyncCompletionCommitManager.PotentialCommitCharacters => commitChars;

        bool IAsyncCompletionCommitManager.ShouldCommitCompletion(char typeChar, SnapshotPoint location, CancellationToken token)
        {
            return true;
        }

        CommitResult IAsyncCompletionCommitManager.TryCommit(ITextView view, ITextBuffer buffer, CompletionItem item, ITrackingSpan applicableSpan, char typedChar, CancellationToken token)
        {
            return CommitResult.Unhandled;
        }
    }
}
