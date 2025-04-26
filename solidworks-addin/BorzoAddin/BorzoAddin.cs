using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Windows.Forms;
using Microsoft.Win32;

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
            // Enable SolidWorks callbacks
            _swApp.SetAddinCallbackInfo2(0, this, _addinID);
            // Add menu/toolbar commands
            var cmdGroup = _swApp.CreateCommandGroup2(_addinID, "Borzo", "Borzo Add-in commands", "", -1);
            int cmdID = cmdGroup.AddCommandItem2("Generate Airfoil", -1, "Generate a NACA airfoil", "GenerateAirfoil", 0, "OnGenerateAirfoil", "", (int)swCommandItemType_e.swMenuItem);
            cmdGroup.HasMenu = true;
            cmdGroup.HasToolbar = true;
            cmdGroup.Activate();
            return true;
        }

        // Disconnect add-in from SolidWorks
        public bool DisconnectFromSW()
        {
            _taskPaneHost?.RemoveTaskPane();
            // TODO: Unsubscribe from events
            return true;
        }

        /// <summary>
        /// Menu/toolbar callback to generate a NACA airfoil
        /// </summary>
        public bool OnGenerateAirfoil()
        {
            var bridge = new CommunicationBridge();
            bridge.GenerateAirfoil("2412", 150.0);
            return true;
        }

        /// <summary>
        /// Registers the add-in in the SolidWorks AddIns registry key.
        /// </summary>
        [ComRegisterFunction]
        public static void RegisterAddin(Type t)
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\SolidWorks\AddIns\" + t.GUID.ToString("B"));
            key.SetValue(null, 1);
            key.SetValue("Description", "Borzo AI SolidWorks Add-in");
            key.SetValue("Title", "Borzo Add-in");
        }

        /// <summary>
        /// Unregisters the add-in from the SolidWorks AddIns registry key.
        /// </summary>
        [ComUnregisterFunction]
        public static void UnregisterAddin(Type t)
        {
            Registry.CurrentUser.DeleteSubKey(@"Software\SolidWorks\AddIns\" + t.GUID.ToString("B"), false);
        }
    }
}
