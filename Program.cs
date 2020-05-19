using System.ServiceProcess;

namespace MyProxy
{
    static class Program
    {
        /// <summary>
        /// Главная точка входа для приложения.
        /// </summary>
        static void Main()
        {
            ServiceBase.Run(new Proxy());
        }
    }
}
