
WPF's Custom UI Editor for Microsoft Office
===============

This GitHub repo is a WPF version of [Microsoft's Custom UI Editor](https://github.com/OfficeDev/office-custom-ui-editor).
Built on Windows Forms, the original editor is useful on its own, but it has some limitations. Rather than trying to
address those limitations by performing small contributions to the original project, this repo offers a complete redesign
of the project in WPF.

Features of this overhauled editor include:
- [ScintillaNET](https://github.com/jacobslusser/ScintillaNET) (via [SctintillaNET.WPF](https://github.com/Stumpii/ScintillaNET.WPF/tree/master/ScintillaNET.WPF)) as text editor, with seamless syntax highlighting
- The TreeView allows you to have more than one file open, easily switching between different customUI files (for example,
for copying code from one file to another)
- List of recently opened files showing up on the file menu (thanks to 
[RecentFileList](https://www.codeproject.com/Articles/23731/RecentFileList-a-WPF-MRU))
- Possibility of reloading a file's contents (i.e. those not viewed in the editor, such as spreadsheet values) before
saving it. This avoids accidental loss of data. For example, an Excel file could be opened in the editor first, then
edited in Excel, then saved in the editor, at which point you would lose those changes
- Possibility of customizing some aspects of the editor such as font size and color
- Plus all the features of the original Windows Forms project

![Screenshot](Screenshot.png)

Build status / Windows executable
---------------------------------

[![Build Status](https://dev.azure.com/fernandreu-public/Custom%20UI%20Editor/_apis/build/status/BuildAndTest?branchName=master)](https://dev.azure.com/fernandreu-public/Custom%20UI%20Editor/_build/latest?definitionId=1&branchName=master)

To download the Windows executable, click on the Artifacts menu in the top-right corner of the link above.

Other info
----------

*This section has been borrowed from the [original Windows Forms project](https://github.com/OfficeDev/office-custom-ui-editor).*

The Office Custom UI Editor is a standalone tool to edit the Custom UI part of Office open document file format. 
It contains both Office 2007 and Office 2010 custom UI schemas.

The Office 2010 custom UI schema is the latest schema and it's still being used in the latest versions of Office including
Office 2013, Office 2016 and Office 365.

To learn more about how to use these identifiers to customize the Office ribbon, backstage, and context menus visit:
 - [Customizing the Office Fluent Ribbon for Developers](https://msdn.microsoft.com/en-us/library/aa338202(v=office.14).aspx)
 - [Introduction to the Office Backstage View for Developers](https://msdn.microsoft.com/en-us/library/ee691833(office.14).aspx)
 - [Office Fluent User Interface Control Identifiers](https://github.com/OfficeDev/office-fluent-ui-command-identifiers)
