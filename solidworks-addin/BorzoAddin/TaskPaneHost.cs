using SolidWorks.Interop.sldworks;
using System;
using System.IO;
using System.Reflection;

namespace BorzoAddin
{
    // Handles Task Pane registration and removal
    public class TaskPaneHost
    {
        private SldWorks _swApp;
        private int _taskPaneTabID;
        private BorzoTaskPaneControl _control;
        private object _taskPane;

        public TaskPaneHost(SldWorks swApp)
        {
            _swApp = swApp;
        }

        public void CreateTaskPane()
        {
            // Register a Task Pane tab with Borzo branding
            // NOTE: This code must be run on Windows with SolidWorks Interop
            // Determine add-in directory and icon path
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var iconPath = Path.Combine(exeDir, "icon.ico");
            _taskPane = _swApp.CreateTaskpaneView2(iconPath, "Borzo AI-Agent");
            // Attach WinForms control to the Task Pane
            var paneView = (ITaskpaneView2)_taskPane;
            _control = new BorzoTaskPaneControl();
            paneView.AddWindowsFormsControlToPane(_control, "Borzo UI");
        }

        public void RemoveTaskPane()
        {
            // Remove the Task Pane
            // TODO: Properly dispose and remove task pane
        }
    }
}
