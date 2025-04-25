using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Windows.Forms;

namespace BorzoAddin
{
    // COM registration attributes
    [ComVisible(true)]
    [Guid("e7b7e2e0-1234-4b1b-abcd-9876543210ab")]
    [ProgId("BorzoAddin.Connect")]
    public class BorzoAddin : ISwAddin
    {
        private SldWorks _swApp;
        private int _addinID;
        private TaskPaneHost _taskPaneHost;

        // Entry point: Connect add-in to SolidWorks
        public bool ConnectToSW(object ThisSW, int cookie)
        {
            _swApp = (SldWorks)ThisSW;
            _addinID = cookie;
            // Register Task Pane
            _taskPaneHost = new TaskPaneHost(_swApp);
            _taskPaneHost.CreateTaskPane();
            // TODO: Subscribe to events (rebuild, selection, insert)
            // TODO: Initialize CommunicationBridge
            return true;
        }

        // Disconnect add-in from SolidWorks
        public bool DisconnectFromSW()
        {
            _taskPaneHost?.RemoveTaskPane();
            // TODO: Unsubscribe from events
            return true;
        }
    }
}
