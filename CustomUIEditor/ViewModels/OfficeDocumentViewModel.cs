﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="OfficeDocumentViewModel.cs" company="FA">
//   Fernando Andreu
// </copyright>
// <summary>
//   Defines the OfficeDocumentViewModel type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace CustomUIEditor.ViewModels
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Windows.Media.Imaging;

    using Data;

    public class OfficeDocumentViewModel : TreeViewItemViewModel
    {
        private OfficeDocument document;
        
        public OfficeDocumentViewModel(OfficeDocument document) 
            : base(null, false, false)
        {
            this.document = document;
            this.LoadParts();
        }

        public OfficeDocument Document => this.document;

        public string Name => Path.GetFileName(this.document.Name);

        /// <summary>
        /// Gets a value indicating whether any of the parts of this document has unsaved changes.
        /// </summary>
        public bool HasUnsavedChanges
        {
            get
            {
                foreach (var child in this.Children)
                {
                    if (child is OfficePartViewModel part && part.HasUnsavedChanges)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public string ImageSource  // TODO: Use the actual ImagesResource file somehow
        {
            get
            {
                switch (this.document.FileType)
                {
                    case OfficeApplications.Excel:
                        return "/Resources/excelwkb.png";
                    case OfficeApplications.PowerPoint:
                        return "/Resources/pptpre.png";
                    case OfficeApplications.Word:
                        return "/Resources/worddoc.png";
                    case OfficeApplications.Xml:
                        return "/Resources/xml.png";
                    default:
                        return null;
                }
            }
        }

        /// <summary>
        /// Reloads the associated Office document, but keeping the OfficeParts currently shown in the GUI. This
        /// ensures that, if the files have been modified externally, the program is still looking at their latest
        /// version. Otherwise, we might accidentally lose those external changes when saving
        /// </summary>
        public void Reload()
        {
            // Store the file name (otherwise, it will have been erased after calling Dispose)
            var fileName = this.document.Name;

            // Dispose current document (not needed as references to its parts are stored in their View models anyway)
            this.document.Dispose();

            // Then, reload it
            this.document = new OfficeDocument(fileName);
            
            // Delete all its original parts
            foreach (XmlParts type in Enum.GetValues(typeof(XmlParts)))
            {
                this.document.RemoveCustomPart(type);
            }

            // Instead, use the parts currently shown in the editor
            foreach (var part in this.Children.OfType<OfficePartViewModel>())
            {
                this.document.SaveCustomPart(part.Part.PartType, part.OriginalContents, true);
                
                // Re-map the Part. This ensures that the PackagePart stored internally in OfficePart points to
                // the right location, in case it is needed
                part.Reload();
            }
        }

        public void Save(bool reloadFirst = false, string fileName = null)
        {
            if (reloadFirst)
            {
                this.Reload();
            }

            // Save each individual part
            foreach (var part in this.Children.OfType<OfficePartViewModel>())
            {
                part.Save();
            }

            // Now save the actual document
            this.document.Save(fileName);
        }

        public void InsertPart(XmlParts type)
        {
            // Check if the part does not exist yet
            var part = this.document.RetrieveCustomPart(type);
            if (part != null)
            {
                return;
            }

            part = this.document.CreateCustomPart(type);
            var partModel = new OfficePartViewModel(part, this);
            this.Children.Add(partModel);
            partModel.IsSelected = true;
        }

        public void RemovePart(XmlParts type)
        {
            this.document.RemoveCustomPart(type);

            for (var i = 0; i < this.Children.Count; ++i)
            {
                if (!(this.Children[i] is OfficePartViewModel part))
                {
                    continue;
                }

                if (part.Part.PartType == type)
                {
                    this.Children.RemoveAt(i);
                    return;
                }
            }
        }

        protected override void LoadChildren()
        {
            this.LoadParts();
        }

        private void LoadParts()
        {
            foreach (var part in this.document.Parts)
            {
                this.Children.Add(new OfficePartViewModel(part, this));
            }
        }
    }
}
