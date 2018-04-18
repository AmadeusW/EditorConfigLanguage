using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Core.Imaging;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Language.Intellisense.AsyncCompletion;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;

namespace EditorConfig
{
    class AsyncCompletionItemSource : IAsyncCompletionSource
    {
        private ITextView _textView;
        private ITextBuffer _buffer;
        private EditorConfigDocument _document;
        private ITextStructureNavigatorSelectorService _navigator;

        public AsyncCompletionItemSource(ITextView textView, ITextStructureNavigatorSelectorService navigator)
        {
            _textView = textView;
            _buffer = textView.TextBuffer; // Assume that there is no projection and EditorConfig buffer is the main buffer.
            _navigator = navigator;
            _document = EditorConfigDocument.FromTextBuffer(_buffer);
        }

        static readonly CompletionFilter StandardFilter = new CompletionFilter("Standard rules", "S", new AccessibleImageId(KnownMonikers.Property.ToImageId(), "Standard rules"));
        static readonly CompletionFilter CsFilter = new CompletionFilter("C# analysis rules", "C", new AccessibleImageId(KnownMonikers.CSFileNode.ToImageId(), "C# rules"));
        static readonly CompletionFilter DotNetFilter = new CompletionFilter(".NET analysis rules", "D", new AccessibleImageId(KnownMonikers.DotNET.ToImageId(), "Dot NET rules"));
        static readonly ImmutableArray<CompletionFilter> StandardFilters = new CompletionFilter[] { StandardFilter }.ToImmutableArray();
        static readonly ImmutableArray<CompletionFilter> CsFilters = new CompletionFilter[] { CsFilter }.ToImmutableArray();
        static readonly ImmutableArray<CompletionFilter> DotNetFilters = new CompletionFilter[] { DotNetFilter }.ToImmutableArray();
        static readonly ImmutableArray<AccessibleImageId> WarningIcons = new AccessibleImageId[] { new AccessibleImageId(KnownMonikers.IntellisenseWarning.ToImageId(), "warning") }.ToImmutableArray();

        async Task<CompletionContext> IAsyncCompletionSource.GetCompletionContextAsync(CompletionTrigger trigger, SnapshotPoint triggerLocation, SnapshotSpan applicableSpan, CancellationToken token)
        {
            SnapshotSpan line = triggerLocation.GetContainingLine().Extent;
            var position = triggerLocation.Position;

            ParseItem prev = _document.ParseItems.LastOrDefault(p => p.Span.Start < position && !p.Span.Contains(position - 1));
            ParseItem parseItem = _document.ItemAtPosition(triggerLocation);

            var list = ImmutableArray.CreateBuilder<CompletionItem>();
            string moniker = null;

            // Property
            if (string.IsNullOrWhiteSpace(line.GetText()) || parseItem?.ItemType == ItemType.Keyword)
            {
                bool isInRoot = !_document.ParseItems.Exists(p => p.ItemType == ItemType.Section && p.Span.Start < position);
                if (isInRoot)
                {
                    if (SchemaCatalog.TryGetKeyword(SchemaCatalog.Root, out Keyword root))
                        list.Add(CreateCompletion(root, root.Category));
                }
                else
                {
                    IEnumerable<Keyword> properties = EditorConfigPackage.CompletionOptions.ShowHiddenKeywords ? SchemaCatalog.AllKeywords : SchemaCatalog.VisibleKeywords;
                    IEnumerable<Keyword> items = properties.Where(i => i.Name != SchemaCatalog.Root);

                    foreach (Keyword property in items)
                        list.Add(CreateCompletion(property, property.Category));
                }

                moniker = "keyword";
            }

            // Value
            else if (parseItem?.ItemType == ItemType.Value)
            {
                if (SchemaCatalog.TryGetKeyword(prev.Text, out Keyword item))
                {
                    if (!item.SupportsMultipleValues && parseItem.Text.Contains(","))
                    {
                        return CompletionContext.Default;
                    }

                    foreach (Value value in item.Values)
                        list.Add(CreateCompletion(value, iconAutomation: "value"));
                }

                moniker = "value";
            }

            // Severity
            else if ((position > 0 && triggerLocation.Snapshot.Length > 1 && triggerLocation.Snapshot.GetText(position - 1, 1) == ":") || parseItem?.ItemType == ItemType.Severity)
            {
                if (prev?.ItemType == ItemType.Value || parseItem?.ItemType == ItemType.Severity)
                {
                    Property prop = _document.PropertyAtPosition(prev.Span.Start);
                    if (SchemaCatalog.TryGetKeyword(prop?.Keyword?.Text, out Keyword key) && key.RequiresSeverity)
                    {
                        foreach (Severity severity in SchemaCatalog.Severities)
                        {
                            list.Add(CreateCompletion(severity));
                        }
                    }

                    moniker = "severity";
                }
            }

            // Suppression
            else if (parseItem?.ItemType == ItemType.Suppression)
            {
                foreach (Error code in ErrorCatalog.All.OrderBy(e => e.Code))
                    list.Add(CreateCompletion(code));

                moniker = "suppression";
            }

            if (!list.Any())
            {
                if (SchemaCatalog.TryGetKeyword(prev?.Text, out Keyword property))
                {
                    int eq = line.GetText().IndexOf("=");

                    if (eq != -1)
                    {
                        int eqPos = eq + line.Start.Position;

                        if (position > eqPos)
                            foreach (Value value in property.Values)
                                list.Add(CreateCompletion(value));
                    }

                    moniker = "value";
                }
            }

            return new CompletionContext(list.ToImmutable());
        }

        async Task<object> IAsyncCompletionSource.GetDescriptionAsync(CompletionItem item, CancellationToken token)
        {
            if (item.Properties.TryGetProperty("item", out ITooltip editorConfigItem) &&
                !string.IsNullOrEmpty(editorConfigItem.Description))
            {
                // Note, this tooltip will work only on Windows.
                // To get tooltips to work on VS:mac, use elements understood by Microsoft.VisualStudio.Text.Adornments.IViewElementFactoryService
                return new Shared.EditorTooltip(editorConfigItem);
            }

            return null;
        }

        bool IAsyncCompletionSource.TryGetApplicableSpan(char typeChar, SnapshotPoint triggerLocation, out SnapshotSpan applicableSpan)
        {
            applicableSpan = default(SnapshotSpan);

            if (!char.IsLetterOrDigit(typeChar) || !EditorConfigPackage.Language.Preferences.AutoListMembers)
                return false;

            ITrackingSpan trackingSpan = FindTokenSpanAtPosition(triggerLocation);
            SnapshotSpan span = trackingSpan.GetSpan(triggerLocation.Snapshot);
            string text = span.GetText();

            if (text == ":" || text == ",")
                applicableSpan = new SnapshotSpan(triggerLocation.Snapshot, span.Start + 1, 0);
            else if (!string.IsNullOrWhiteSpace(text))
                applicableSpan = span;
            else
                applicableSpan = new SnapshotSpan(triggerLocation, 0); // TODO: see why this method doesn't get called on a bare line

            return true;
        }

        private ITrackingSpan FindTokenSpanAtPosition(SnapshotPoint position)
        {
            ParseItem item = _document.ItemAtPosition(position);

            if (item != null)
            {
                return _textView.TextSnapshot.CreateTrackingSpan(item.Span, SpanTrackingMode.EdgeInclusive);
            }
            else
            {
                int offset = position > 0 ? -1 : 0;
                SnapshotPoint currentPoint = position + offset;
                ITextStructureNavigator navigator = _navigator.GetTextStructureNavigator(_buffer);
                TextExtent extent = navigator.GetExtentOfWord(currentPoint);
                return currentPoint.Snapshot.CreateTrackingSpan(extent.Span, SpanTrackingMode.EdgeInclusive);
            }
        }

        private CompletionItem CreateCompletion(ITooltip item, Category category = Category.None, string iconAutomation = null)
        {
            ImmutableArray<AccessibleImageId> extraIcons = ImmutableArray<AccessibleImageId>.Empty;
            string automationText;
            ImmutableArray<CompletionFilter> filters;
            string displayText = item.Name;

            if (int.TryParse(item.Name, out int integer))
                displayText = "<integer>";

            if (!item.IsSupported)
                extraIcons = WarningIcons;

            switch (category)
            {
                case Category.CSharp:
                    filters = CsFilters;
                    automationText = category.ToString();
                    break;
                case Category.DotNet:
                    filters = DotNetFilters;
                    automationText = category.ToString();
                    break;
                case Category.Standard:
                    filters = StandardFilters;
                    automationText = category.ToString();
                    break;
                default:
                    filters = ImmutableArray<CompletionFilter>.Empty;
                    automationText = iconAutomation ?? string.Empty;
                    break;
            }
            var completion = new CompletionItem(displayText, this, new AccessibleImageId(item.Moniker.ToImageId(), automationText), filters, string.Empty, item.Name, item.Name, item.Name, extraIcons);
            completion.Properties.AddProperty("item", item);

            return completion;
        }
    }
}
