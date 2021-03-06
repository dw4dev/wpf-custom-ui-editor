﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MainWindowViewModel.cs" company="FA">
//   Fernando Andreu
// </copyright>
// <summary>
//   Defines the MainWindowViewModel type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace CustomUIEditor.ViewModels
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Windows;
    using System.Xml;
    using System.Xml.Schema;

    using CustomUIEditor.Data;
    using CustomUIEditor.Extensions;
    using CustomUIEditor.Services;

    using Prism.Commands;
    using Prism.Mvvm;

    public class MainWindowViewModel : BindableBase
    {
        private readonly IMessageBoxService messageBoxService;

        private readonly IFileDialogService fileDialogService;

        /// <summary>
        /// Whether documents should be reloaded right before being saved.
        /// </summary>
        private bool reloadOnSave = true;
        
        private Hashtable customUiSchemas;

        private TreeViewItemViewModel selectedItem = null;

        /// <summary>
        /// Used during the XML validation to flag whether there was any error during the process
        /// </summary>
        private bool hasXmlError;

        public MainWindowViewModel(IMessageBoxService messageBoxService, IFileDialogService fileDialogService)
        {
            this.messageBoxService = messageBoxService;
            this.fileDialogService = fileDialogService;

            this.OpenCommand = new DelegateCommand(this.OpenFile);
            this.SaveCommand = new DelegateCommand(this.Save);
            this.SaveAllCommand = new DelegateCommand(this.SaveAll);
            this.SaveAsCommand = new DelegateCommand(this.SaveAs);
            this.CloseCommand = new DelegateCommand(this.CloseDocument);
            this.InsertXml14Command = new DelegateCommand(() => this.CurrentDocument?.InsertPart(XmlParts.RibbonX14));
            this.InsertXml12Command = new DelegateCommand(() => this.CurrentDocument?.InsertPart(XmlParts.RibbonX12));
            this.InsertXmlSampleCommand = new DelegateCommand<string>(this.InsertXmlSample);
            this.InsertIconsCommand = new DelegateCommand(this.InsertIcons);
            this.ChangeIconIdCommand = new DelegateCommand(this.ChangeIconId);
            this.RemoveCommand = new DelegateCommand(this.RemoveItem);
            this.ValidateCommand = new DelegateCommand(() => this.ValidateXml(true));
            this.ShowSettingsCommand = new DelegateCommand(() => this.ShowSettings?.Invoke(this, EventArgs.Empty));
            this.RecentFileClickCommand = new DelegateCommand<string>(this.FinishOpeningFile);
            this.ClosingCommand = new DelegateCommand<CancelEventArgs>(this.QueryClose);
            
            var applicationFolder = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
#if DEBUG
            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                return;
            }
#endif
            this.LoadXmlSchemas(applicationFolder + @"\Schemas\");
            this.LoadXmlSamples(applicationFolder + @"\Samples\");
        }

        public event EventHandler ShowSettings;

        public event EventHandler<DataEventArgs<string>> UpdateEditor;

        public event EventHandler<DataEventArgs<string>> InsertRecentFile;

        public event EventHandler<DataEventArgs<string>> ReadCurrentText;
        
        public ObservableCollection<OfficeDocumentViewModel> DocumentList { get; } = new ObservableCollection<OfficeDocumentViewModel>();

        public ObservableCollection<XmlSampleViewModel> XmlSamples { get; } = new ObservableCollection<XmlSampleViewModel>();

        /// <summary>
        /// Gets or sets a value indicating whether documents should be reloaded right before being saved.
        /// </summary>
        public bool ReloadOnSave
        {
            get => this.reloadOnSave;
            set => this.SetProperty(ref this.reloadOnSave, value);
        }

        public TreeViewItemViewModel SelectedItem
        {
            get => this.selectedItem;
            set
            {
                this.ApplyCurrentText();
                this.SetProperty(
                    ref this.selectedItem,
                    value,
                    () =>
                        {
                            if (this.SelectedItem != null)
                            {
                                this.SelectedItem.IsSelected = true;
                            }

                            this.RaisePropertyChanged(nameof(this.CurrentDocument));
                            this.RaisePropertyChanged(nameof(this.IsDocumentSelected));
                            this.RaisePropertyChanged(nameof(this.IsPartSelected));
                            this.RaisePropertyChanged(nameof(this.IsIconSelected));
                            this.RaisePropertyChanged(nameof(this.CanInsertXml12Part));
                            this.RaisePropertyChanged(nameof(this.CanInsertXml14Part));
                        });
            }
        }

        public bool IsDocumentSelected => this.SelectedItem is OfficeDocumentViewModel;

        public bool IsPartSelected => this.SelectedItem is OfficePartViewModel;

        public bool IsIconSelected => this.SelectedItem is IconViewModel;
        
        public bool CanInsertXml12Part => (this.SelectedItem is OfficeDocumentViewModel model) && model.Document.RetrieveCustomPart(XmlParts.RibbonX12) == null;

        public bool CanInsertXml14Part => (this.SelectedItem is OfficeDocumentViewModel model) && model.Document.RetrieveCustomPart(XmlParts.RibbonX14) == null;

        public DelegateCommand OpenCommand { get; }

        public DelegateCommand SaveCommand { get; }

        public DelegateCommand SaveAllCommand { get; }

        public DelegateCommand SaveAsCommand { get; }

        public DelegateCommand CloseCommand { get; }

        public DelegateCommand InsertXml14Command { get; }
        
        public DelegateCommand InsertXml12Command { get; }

        public DelegateCommand<string> InsertXmlSampleCommand { get; set; }

        public DelegateCommand InsertIconsCommand { get; }

        public DelegateCommand ChangeIconIdCommand { get; }

        public DelegateCommand RemoveCommand { get; }

        public DelegateCommand ValidateCommand { get; }

        public DelegateCommand ShowSettingsCommand { get; }

        public DelegateCommand<string> RecentFileClickCommand { get; }

        public DelegateCommand<CancelEventArgs> ClosingCommand { get; }

        /// <summary>
        /// Gets the View model of the OfficeDocument currently active (selected) on the application
        /// </summary>
        public OfficeDocumentViewModel CurrentDocument
        {
            get
            {
                // Get currently active document
                if (!(this.SelectedItem is TreeViewItemViewModel elem))
                {
                    return null;
                }

                // Find the root document
                if (elem is IconViewModel)
                {
                    return (OfficeDocumentViewModel)elem.Parent.Parent;
                }

                if (elem is OfficePartViewModel)
                {
                    return (OfficeDocumentViewModel)elem.Parent;
                }

                if (elem is OfficeDocumentViewModel)
                {
                    return (OfficeDocumentViewModel)elem;
                }

                return null;
            }
        }

        private void CloseDocument()
        {
            var doc = this.CurrentDocument;
            if (doc == null)
            {
                // Nothing to close
                return;
            }

            if (doc.HasUnsavedChanges)
            {
                var result = this.messageBoxService.Show(string.Format(StringsResource.idsCloseWarningMessage, doc.Name), StringsResource.idsCloseWarningTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    this.SaveCommand.Execute();
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            doc.Document.Dispose();
            this.DocumentList.Remove(doc);
        }

        private void ApplyCurrentText()
        {
            if (this.SelectedItem == null || !this.SelectedItem.CanHaveContents)
            {
                return;
            }

            var e = new DataEventArgs<string>();
            this.ReadCurrentText?.Invoke(this, e);
            if (e.Data == null)
            {
                // This means that event handler was not listened by any view, or the view did not pass the editor contents back for some reason
                return;
            }
            
            this.SelectedItem.Contents = e.Data;
        }

        private void InsertIcons()
        {
            if (!(this.SelectedItem is OfficePartViewModel))
            {
                return;
            }

            this.fileDialogService.OpenFilesDialog(StringsResource.idsInsertIconsDialogTitle, StringsResource.idsFilterAllSupportedImages + "|" + StringsResource.idsFilterAllFiles, this.FinishInsertingIcons);
        }

        /// <summary>
        /// This method does not change the icon Id per se, just enables the possibility of doing so in the view
        /// </summary>
        private void ChangeIconId()
        {
            if (!(this.SelectedItem is IconViewModel icon))
            {
                return;
            }

            icon.IsEditingId = true;
        }

        private void FinishInsertingIcons(IEnumerable<string> filePaths)
        {
            if (!(this.SelectedItem is OfficePartViewModel part))
            {
                // If OpenFileDialog opens modally, this should not happen
                return;
            }

            foreach (var path in filePaths)
            {
                part.InsertIcon(path);
            }
        }

        private void RemoveItem()
        {
            if (this.SelectedItem is OfficePartViewModel)
            {
                var part = (OfficePartViewModel)this.SelectedItem;
                var doc = (OfficeDocumentViewModel)part.Parent;
                doc.RemovePart(part.Part.PartType);
                return;
            }

            if (this.SelectedItem is IconViewModel icon)
            {
                var part = (OfficePartViewModel)icon.Parent;
                part.RemoveIcon(icon.Id);
            }
        }

        private void QueryClose(CancelEventArgs e)
        {
            this.ApplyCurrentText();
            foreach (var doc in this.DocumentList)
            {
                if (doc.HasUnsavedChanges)
                {
                    var result = this.messageBoxService.Show(string.Format(StringsResource.idsCloseWarningMessage, doc.Name), StringsResource.idsCloseWarningTitle, MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
                    if (result == MessageBoxResult.Yes)
                    {
                        this.SaveCommand.Execute();
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }

            // Now that it is clear we can leave the program, dispose all documents (i.e. delete the temporary unzipped files)
            foreach (var doc in this.DocumentList)
            {
                doc.Document.Dispose();
            }
        }

        private void OpenFile()
        {
            string[] filters =
                {
                    StringsResource.idsFilterAllOfficeDocuments,
                    StringsResource.idsFilterWordDocuments,
                    StringsResource.idsFilterExcelDocuments,
                    StringsResource.idsFilterPPTDocuments,
                    StringsResource.idsFilterAllFiles,
                };

            this.fileDialogService.OpenFileDialog(StringsResource.idsOpenDocumentDialogTitle, string.Join("|", filters), this.FinishOpeningFile);
        }

        private void FinishOpeningFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            try
            {
                Debug.WriteLine("Opening " + fileName + "...");

                var doc = new OfficeDocument(fileName);
                var model = new OfficeDocumentViewModel(doc);
                if (model.Children.Count > 0)
                {
                    model.Children[0].IsSelected = true;
                }

                this.DocumentList.Add(model);
                this.InsertRecentFile?.Invoke(this, new DataEventArgs<string> { Data = fileName });
                
                // UndoRedo
                ////_commands = new UndoRedo.Control.Commands(rtbCustomUI.Rtf);
            }
            catch (Exception ex)
            {
                this.messageBoxService.Show(ex.Message, "Error opening Office document", image: MessageBoxImage.Error);
            }
        }

        private void Save()
        {
            this.ApplyCurrentText();
            this.CurrentDocument?.Save(this.ReloadOnSave);
        }

        private void SaveAll()
        {
            this.ApplyCurrentText();
            foreach (var doc in this.DocumentList)
            {
                doc.Save(this.ReloadOnSave);
            }
        }

        private void SaveAs()
        {
            var doc = this.CurrentDocument;
            if (doc == null)
            {
                return;
            }
            
            var filters = new List<string>();
            for (;;)
            {
                var filter = StringsResource.ResourceManager.GetString("idsFilterSaveAs" + filters.Count);
                if (filter == null)
                {
                    break;
                }

                filters.Add(filter);
            }

            filters.Add(StringsResource.idsFilterAllFiles);
            
            var ext = Path.GetExtension(doc.Name);

            // Find the appropriate FilterIndex
            int i;
            for (i = 0; i < filters.Count - 1; i++)
            {
                // -1 to exclude all files
                var otherExt = filters[i].Split('|')[1].Substring(1);
                if (ext == otherExt)
                {
                    break;
                }
            }

            this.fileDialogService.SaveFileDialog(StringsResource.idsSaveDocumentAsDialogTitle, string.Join("|", filters), this.FinishSavingFile, doc.Name, i + 1);
        }

        private void FinishSavingFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            try
            {
                // Note: We are assuming that no UI events happen between the SaveFileDialog was
                // shown and this is called. Otherwise, selection might have changed
                var doc = this.CurrentDocument;
                Debug.Assert(doc != null, "Selected document seems to have changed between showing file dialog and closing it");

                if (!Path.HasExtension(fileName))
                {
                    fileName = Path.ChangeExtension(fileName, Path.GetExtension(doc.Name));
                }

                Debug.WriteLine("Saving " + fileName + "...");

                doc.Save(this.reloadOnSave, fileName);
                this.InsertRecentFile?.Invoke(this, new DataEventArgs<string> { Data = fileName });
            }
            catch (Exception ex)
            {
                this.messageBoxService.Show(ex.Message, "Error saving Office document", image: MessageBoxImage.Error);
            }
        }

        private void LoadXmlSchemas(string folderName)
        {
            if (string.IsNullOrEmpty(folderName))
            {
                Debug.Print("path is null / empty");
                return;
            }

            try
            {
                var schemas = Directory.GetFiles(folderName, "CustomUI*.xsd");

                if (schemas.Length == 0)
                {
                    return;
                }

                this.customUiSchemas = new Hashtable(schemas.Length);

                foreach (var schema in schemas)
                {
                    var partType = schema.Contains("14") ? XmlParts.RibbonX14 : XmlParts.RibbonX12;
                    var reader = new StreamReader(schema);
                    this.customUiSchemas.Add(partType, XmlSchema.Read(reader, null));

                    reader.Close();
                }
            }
            catch (Exception ex)
            {
                Debug.Fail(ex.Message);
            }
        }

        private void LoadXmlSamples(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.Print("path is null / empty");
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(path, "*.xml");
            }
            catch (IOException ex)
            {
                Debug.Fail(ex.Message);
                return;
            }

            foreach (var file in files)
            {
                this.XmlSamples.Add(new XmlSampleViewModel { FilePath = file });
            }
        }

        private void InsertXmlSample(string path)
        {
            Debug.Assert(!string.IsNullOrEmpty(path), "Path not passed");
            
            if (this.SelectedItem is OfficeDocumentViewModel doc)
            {
                // See if there is already a part, and otherwise insert one
                if (doc.Children.Count == 0)
                {
                    doc.InsertPart(XmlParts.RibbonX12);
                }

                this.SelectedItem = doc.Children[0];
            }
            
            if (!(this.SelectedItem is OfficePartViewModel part))
            {
                return;
            }
            
            // TODO: Show message box for confirmation
            try
            {
                using (var sr = new StreamReader(path))
                {
                    // TODO: This should be automatically raised by the ViewModel when setting the part contents
                    this.UpdateEditor?.Invoke(this, new DataEventArgs<string> { Data = sr.ReadToEnd() });
                }
            }
            catch (Exception ex)
            {
                Debug.Assert(false, ex.Message);
                Debug.Fail(ex.Message);
            }
        }

        private bool ValidateXml(bool showValidMessage)
        {
            if (!(this.SelectedItem is OfficePartViewModel part))
            {
                return false;
            }
            
            this.ApplyCurrentText();

            // Test to see if text is XML first
            try
            {
                var xmlDoc = new XmlDocument();

                if (!(this.customUiSchemas[part.Part.PartType] is XmlSchema targetSchema))
                {
                    return false;
                }

                xmlDoc.Schemas.Add(targetSchema);

                xmlDoc.LoadXml(part.Contents);

                if (xmlDoc.DocumentElement == null)
                {
                    // TODO: ShowError call with an actual message perhaps? Will this ever be null
                    return false;
                }

                if (xmlDoc.DocumentElement.NamespaceURI != targetSchema.TargetNamespace)
                {
                    var errorText = new StringBuilder();
                    errorText.Append(string.Format(StringsResource.idsUnknownNamespace, xmlDoc.DocumentElement.NamespaceURI));
                    errorText.Append("\n" + string.Format(StringsResource.idsCustomUINamespace, targetSchema.TargetNamespace));

                    this.messageBoxService.Show(errorText.ToString(), "Error validating XML", image: MessageBoxImage.Error);
                    return false;
                }

                this.hasXmlError = false;
                xmlDoc.Validate(this.XmlValidationEventHandler);
            }
            catch (XmlException ex)
            {
                this.messageBoxService.Show(StringsResource.idsInvalidXml + "\n" + ex.Message, "Error validating XML", image: MessageBoxImage.Error);
                return false;
            }
            
            if (!this.hasXmlError)
            {
                if (showValidMessage)
                {
                    this.messageBoxService.Show(
                        StringsResource.idsValidXml,
                        "XML is valid",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return true;
            }

            return false;
        }
        
        private void XmlValidationEventHandler(object sender, ValidationEventArgs e)
        {
            lock (this)
            {
                this.hasXmlError = true;
            }

            this.messageBoxService.Show(
                e.Message,
                e.Severity.ToString(),
                MessageBoxButton.OK,
                e.Severity == XmlSeverityType.Error ? MessageBoxImage.Error : MessageBoxImage.Warning);
        }
    }
}
