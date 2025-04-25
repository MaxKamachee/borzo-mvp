using System;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Windows.Forms;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace BorzoAddin
{
    // Communication bridge for HTTP calls to FastAPI backend
    // Allow JavaScript in the WebBrowser to call methods on this class
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class CommunicationBridge
    {
        private readonly HttpClient _client;
        private readonly AirfoilSketcher _sketcher;

        public CommunicationBridge()
        {
            _client = new HttpClient();
            _sketcher = new AirfoilSketcher();
        }

        /// <summary>
        /// Send a JSON payload to the specified FastAPI endpoint and return raw response.
        /// </summary>
        public async Task<string> SendMessageAsync(string endpoint, string jsonPayload)
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            var response = await _client.PostAsync("http://localhost:8000" + endpoint, content);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Convenience method to classify natural-language text via /classify endpoint.
        /// </summary>
        public Task<string> ClassifyAsync(string text)
        {
            var payload = $"{{\"text\":\"{text}\"}}";
            return SendMessageAsync("/classify", payload);
        }

        /// <summary>
        /// Generate a NACA airfoil sketch in SolidWorks via AirfoilSketcher.
        /// </summary>
        public void GenerateAirfoil(string nacaCode, double chordMm)
        {
            // Fetch coordinates from FastAPI
            var payload = JsonSerializer.Serialize(new { naca = nacaCode, chord = chordMm });
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = _client.PostAsync("http://localhost:8000/airfoil", content).GetAwaiter().GetResult();
            resp.EnsureSuccessStatusCode();
            var json = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            using var doc = JsonDocument.Parse(json);
            var coordsElem = doc.RootElement.GetProperty("coords");
            int cnt = coordsElem.GetArrayLength();
            var arr = new double[cnt * 3];
            for (int i = 0; i < cnt; i++)
            {
                var pt = coordsElem[i];
                arr[i*3] = pt.GetProperty("x").GetDouble();
                arr[i*3 + 1] = pt.GetProperty("y").GetDouble();
                arr[i*3 + 2] = 0.0;
            }
            // Draw spline from external coords
            _sketcher.SketchCoords(arr);
        }

        /// <summary>
        /// Inserts a STEP component via AirfoilSketcher.
        /// </summary>
        public void InsertStep(string filename)
        {
            _sketcher.InsertStep(filename);
        }

        /// <summary>
        /// Returns live center-of-gravity info as JSON.
        /// </summary>
        public string GetCG()
        {
            return _sketcher.GetCG();
        }

        /// <summary>
        /// Returns design rule check violations as JSON.
        /// </summary>
        public string CheckDRC(string partId)
        {
            var payload = JsonSerializer.Serialize(new { part_id = partId });
            return SendMessageAsync("/drc", payload).GetAwaiter().GetResult();
        }
    }
}
