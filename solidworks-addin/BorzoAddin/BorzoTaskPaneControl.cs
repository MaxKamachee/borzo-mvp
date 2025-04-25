using System.Windows.Forms;
using System.IO;
using System.Reflection;

namespace BorzoAddin
{
    // UserControl for Borzo Task Pane, loads React UI via WebBrowser
    public class BorzoTaskPaneControl : UserControl
    {
        private WebBrowser _webBrowser;
        private CommunicationBridge _bridge;

        public BorzoTaskPaneControl()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // initialize communication bridge for JS interop
            _bridge = new CommunicationBridge();
            _webBrowser = new WebBrowser();
            _webBrowser.ObjectForScripting = _bridge;
            _webBrowser.ScriptErrorsSuppressed = true;
            _webBrowser.IsWebBrowserContextMenuEnabled = false;
            _webBrowser.AllowWebBrowserDrop = false;
            // optional: react to document load
            _webBrowser.DocumentCompleted += (s, e) => { /* JS context ready */ };
            _webBrowser.Dock = DockStyle.Fill;
            // Load React UI: dev (localhost) or prod (client/build)
            var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var prodHtml = Path.Combine(exeDir, "client", "build", "index.html");
            if (File.Exists(prodHtml)) {
                _webBrowser.Url = new Uri(prodHtml);
            } else {
                _webBrowser.Url = new Uri("http://localhost:3000");
            }
            this.Controls.Add(_webBrowser);
        }
    }
}
