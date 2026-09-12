using System;
using System.Text;
using System.Windows.Forms;

namespace ApiTester
{
    static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.ThreadException += (s, e) =>
            {
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"), e.Exception.ToString()); } catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try { System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "crash.log"), e.ExceptionObject?.ToString()); } catch { }
            };
            //Legacy code pages - windows-1250 above all - are not in the box. Registered here
            //so Encoding.GetEncoding resolves whatever charset a request header asks for and
            //whatever a response declares, instead of falling back to UTF-8 and mangling it.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Application.EnableVisualStyles();
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
            Application.Run(new Form1());
        }
    }
}
