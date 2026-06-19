using System;
// using IntegrationFlow.ExtensionsPoints;

namespace IntegrationFlow.Contexts.Integrations.Scheduler
{
    internal class IntegrationScheduler
    {
        internal void Run()
        {
            try
            {
                //System.Diagnostics.Debugger.Launch();
                
                // // Запустить / перезапустить все интеграции типа "Получить и обработать"
                // // (реализации точки расширения IReceiveAndProcessLauncher)
                // var receiveAndProcessLaunchers = ComponentManager.Current.GetExtensionPoints<IReceiveAndProcessLauncher>();
                // foreach (var receiveAndProcessLauncher in receiveAndProcessLaunchers)
                // {
                //     receiveAndProcessLauncher.Run();
                // }
            }
            catch (Exception ex)
            {
            }
        }
    }
}
