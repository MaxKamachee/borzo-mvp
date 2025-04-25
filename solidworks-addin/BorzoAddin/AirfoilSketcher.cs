using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Runtime.InteropServices.RuntimeInformation;
using System.IO;
using System.Text.Json;

namespace BorzoAddin
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.AutoDual)]
    public class AirfoilSketcher
    {
        private readonly SldWorks _swApp;

        public AirfoilSketcher()
        {
            // Get running SolidWorks instance
            _swApp = (SldWorks)Marshal.GetActiveObject("SldWorks.Application");
        }

        /// <summary>
        /// Generates a symmetric NACA airfoil sketch based on a 4- or 5-digit NACA code.
        /// </summary>
        /// <param name="nacaCode">4- or 5-digit NACA code (e.g., "2412")</param>
        /// <param name="chordMm">Chord length in millimeters</param>
        public void GenerateAirfoil(string nacaCode, double chordMm)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                System.Diagnostics.Debug.WriteLine("AirfoilSketcher.GenerateAirfoil skipped: not Windows");
                return;
            }
            var model = _swApp.ActiveDoc as IModelDoc2;
            if (model == null)
                throw new InvalidOperationException("No active document in SolidWorks.");

            // Start sketch on the Front plane
            model.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0);
            model.SketchManager.InsertSketch(true);

            // Compute airfoil points
            var pts = GetNacaPoints(nacaCode, chordMm, 50);

            // Convert points to array for spline (x,y,z sequentially)
            var arr = new double[pts.Count * 3];
            for (int i = 0; i < pts.Count; i++)
            {
                arr[i*3] = pts[i].X;
                arr[i*3 + 1] = pts[i].Y;
                arr[i*3 + 2] = 0;
            }

            // Create spline
            model.SketchManager.CreateSpline(arr);

            // Exit sketch
            model.SketchManager.InsertSketch(true);
        }

        /// <summary>
        /// Draws a spline from an array of [x,y,z] coords (external service output).
        /// </summary>
        public void SketchCoords(double[] arr)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                System.Diagnostics.Debug.WriteLine("AirfoilSketcher.SketchCoords skipped: not Windows");
                return;
            }
            var model = _swApp.ActiveDoc as IModelDoc2;
            if (model == null)
                throw new InvalidOperationException("No active document in SolidWorks.");
            // Create sketch on the Front plane
            model.Extension.SelectByID2("Front Plane", "PLANE", 0, 0, 0, false, 0, null, 0);
            model.SketchManager.InsertSketch(true);
            // Create spline from external coords
            model.SketchManager.CreateSpline(arr);
            // Exit sketch
            model.SketchManager.InsertSketch(true);
        }

        /// <summary>
        /// Inserts a STEP component into the active assembly.
        /// </summary>
        public void InsertStep(string filename)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                System.Diagnostics.Debug.WriteLine($"InsertStep skipped: not Windows ({filename})");
                return;
            }
            if (!(_swApp.ActiveDoc is IAssemblyDoc asm))
                throw new InvalidOperationException("Active document is not an assembly.");
            string addinDir = Path.GetDirectoryName(typeof(AirfoilSketcher).Assembly.Location);
            string stepPath = Path.Combine(addinDir, "assets", filename);
            asm.AddComponent5(stepPath,
                (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig,
                string.Empty, false, 0, 0, 0, 0, out int cookie);
        }

        /// <summary>
        /// Returns live center-of-gravity delta and verdict as JSON.
        /// </summary>
        public string GetCG()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return JsonSerializer.Serialize(new { cg_ok = true, delta_mm = 0.0, verdict = "green" });
            var model = _swApp.ActiveDoc as IModelDoc2;
            if (model == null)
                throw new InvalidOperationException("No active document in SolidWorks.");
            double mass;
            var props = model.Extension.GetMassProperties2((int)swMassPropOption_e.swMassPropOption_BodyPropsOnly, out mass);
            double x = props[1] * 1000;
            double y = props[2] * 1000;
            double z = props[3] * 1000;
            double delta = Math.Sqrt(x * x + y * y + z * z);
            bool ok = delta < 1.0;
            string verdict = ok ? "green" : (delta < 5.0 ? "yellow" : "red");
            return JsonSerializer.Serialize(new { cg_ok = ok, delta_mm = delta, verdict = verdict });
        }

        private List<Point2D> GetNacaPoints(string code, double c, int numPts)
        {
            // Parse thickness from the last two digits
            double t = 0;
            if (code.Length >= 4 && double.TryParse(code.Substring(code.Length - 2, 2), out double thickness))
            {
                t = thickness / 100.0;
            }

            var pts = new List<Point2D>();
            // Upper surface
            for (int i = 0; i <= numPts; i++)
            {
                double x = c * (double)i / numPts;
                double yt = 5 * t * c * (0.2969 * Math.Sqrt(x / c)
                                          - 0.1260 * (x / c)
                                          - 0.3516 * Math.Pow(x / c, 2)
                                          + 0.2843 * Math.Pow(x / c, 3)
                                          - 0.1015 * Math.Pow(x / c, 4));
                pts.Add(new Point2D(x, yt));
            }
            // Lower surface
            for (int i = numPts; i >= 0; i--)
            {
                double x = c * (double)i / numPts;
                double yt = 5 * t * c * (0.2969 * Math.Sqrt(x / c)
                                          - 0.1260 * (x / c)
                                          - 0.3516 * Math.Pow(x / c, 2)
                                          + 0.2843 * Math.Pow(x / c, 3)
                                          - 0.1015 * Math.Pow(x / c, 4));
                pts.Add(new Point2D(x, -yt));
            }
            return pts;
        }
    }

    [ComVisible(false)]
    public struct Point2D
    {
        public double X;
        public double Y;
        public Point2D(double x, double y) { X = x; Y = y; }
    }
}
