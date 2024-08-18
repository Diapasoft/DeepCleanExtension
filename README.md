VS extension to clean all 'bin' and 'obj' directories created during project compilation.

The standard VS "Clean" command does not delete all of these items nor are they always recreated during project rebuild.
This extension can be useful for solving problems with IntelliSense not recognizing methods or files (this sometimes happens with WPF or MAUI projects, e.g. "InitializeComponent()" method not found).
