using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Utilities;

namespace EditorConfig
{
    [Export(typeof(IAsyncCompletionSourceProvider))]
    [Name(nameof(Constants.FeatureName))]
    [ContentType(Constants.LanguageName)]
    class AsyncCompletionItemSourceProvider : IAsyncCompletionSourceProvider
    {
        IDictionary<ITextView, AsyncCompletionItemSource> cache = new Dictionary<ITextView, AsyncCompletionItemSource>();

        [Import]
        ITextStructureNavigatorSelectorService NavigatorService { get; set; }

        IAsyncCompletionSource IAsyncCompletionSourceProvider.GetOrCreate(ITextView textView)
        {
            if (cache.TryGetValue(textView, out var itemSource))
                return itemSource;

            var source = new AsyncCompletionItemSource(textView, NavigatorService);
            textView.Closed += (o, e) =>
            {
                cache.Remove(textView);
            };
            cache.Add(textView, source);
            return source;
        }
    }

}
