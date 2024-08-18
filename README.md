**VS extension to clean all 'bin' and 'obj' directories created during project compilation.**

The standard VS "Clean" command does not delete all of these items nor are they always recreated during project rebuild.
This extension can be useful for solving problems with IntelliSense not recognizing methods or files (this sometimes happens with WPF or MAUI projects, e.g. "InitializeComponent()" method not found).

Once installed, you'll find DeepCleanExtension commands under "Extensions" menu in VS.

<img width="614" alt="image" src="https://github.com/user-attachments/assets/befe49d7-7dbf-46af-b4e1-c6ab215b474d">

* 1: _Run Deep Clean on all directories for current Solution_ allows you to quickly delete all _bin_ and _obj_ directories in your currently open Solution.
* 2: _Run Deep Clean on all directories for current Project_ allows you to quickly delete all _bin_ and _obj_ directories in your currently open Project. Note that DeepCleanExtension determine your currently open Project by looking at your currently open file (startup project is not taken into account).
* 3: _Select directories and run Deep Clean_ will list you each _bin_ or _obj_ directory and ask which of them you want to delete.


_Feel free to ask any question, open issues or contribute to this simple extension._


References and examples for issues which can be resolved by this extension:

* https://stackoverflow.com/questions/76662321/initializecomponent-does-not-exist
* https://stackoverflow.com/questions/74601011/net-maui-the-name-initializecomponent-does-not-exist-in-the-current-context
* https://csharpforums.net/threads/how-to-resolve-this-error-the-name-initializecomponent-does-not-exist-in-the-current-context.9391/
