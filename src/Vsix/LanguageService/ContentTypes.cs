﻿using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace EditorConfig
{
    public class ContentTypes
    {
        [Export(typeof(ContentTypes))]
        [Name(Constants.LanguageName)]
        [BaseDefinition("code")]
        public ContentTypes IEditorConfigContentType { get; set; }
    }
}
