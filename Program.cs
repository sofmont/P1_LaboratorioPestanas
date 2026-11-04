using System;
using System.Threading;
using System.Windows.Forms;
using LaboratorioPestanas.UI;

namespace LaboratorioPestanas
{
    /// <summary>
    /// Programa 1 — Laboratorio de búsquedas con interfaz por pestañas y arquitectura en capas
    /// (Núcleo → Experimentos → Interfaz). Tabla hash con ENCADENAMIENTO SEPARADO y método de la división.
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Red de seguridad global: ninguna excepción no controlada debe cerrar la aplicación sin aviso.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnDomainException;

            Application.Run(new MainForm());
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            MessageBox.Show("Error inesperado:\n" + e.Exception.Message, "Laboratorio de búsquedas",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
        {
            string msg = e.ExceptionObject is Exception ex ? ex.Message : "Error desconocido.";
            MessageBox.Show("Error crítico:\n" + msg, "Laboratorio de búsquedas",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
