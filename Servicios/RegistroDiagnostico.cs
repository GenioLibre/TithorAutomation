using System;
using System.IO;
using System.Text;
using System.Diagnostics;

namespace TithorAutomation.Servicios
{
    internal static class RegistroDiagnostico
    {
        internal static string Guardar(Exception error, string contexto)
        {
            try
            {
                string carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TithorAutomation", "Logs");
                Directory.CreateDirectory(carpeta);
                string ruta = Path.Combine(carpeta, "error-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".log");
                var texto = new StringBuilder();
                texto.AppendLine("Fecha: " + DateTimeOffset.Now.ToString("o"));
                texto.AppendLine("Tithor: " + typeof(RegistroDiagnostico).Assembly.FullName);
                texto.AppendLine("Windows: " + Environment.OSVersion + "; CLR: " + Environment.Version + "; proceso64: " + Environment.Is64BitProcess);
                texto.AppendLine("Contexto: " + contexto);
                texto.AppendLine("Excepciones:");
                for (Exception e = error; e != null; e = e.InnerException)
                    texto.AppendLine(e.GetType().FullName + " HRESULT=0x" + e.HResult.ToString("X8") + ": " + e.Message);
                texto.AppendLine(error.ToString());
                // Versiones reales de las DLL instaladas, sin consultar nuevamente COM.
                foreach (string archivo in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory, "*.dll"))
                {
                    try { texto.AppendLine(Path.GetFileName(archivo) + ": " + FileVersionInfo.GetVersionInfo(archivo).FileVersion); }
                    catch { }
                }
                File.WriteAllText(ruta, texto.ToString(), Encoding.UTF8);
                return "Registro: " + ruta;
            }
            catch (Exception ex)
            {
                return "No se pudo escribir el registro: " + ex.Message;
            }
        }
    }
}
