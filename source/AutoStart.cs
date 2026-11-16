using System;
using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;

namespace LenovoController
{
    /// <summary>
    ///     Manages the Windows Task Scheduler entry used to launch the app at sign-in.
    ///     A scheduled task is used instead of a Run key so the elevated app starts without a UAC prompt.
    /// </summary>
    internal static class AutoStart
    {
        private const string TaskName = "LenovoController";
        private const string TaskFolder = "\\";

        // Task Scheduler COM constants.
        private const int TaskCreateOrUpdate = 6;
        private const int TaskLogonInteractiveToken = 3;
        private const int TaskTriggerLogon = 9;
        private const int TaskActionExec = 0;
        private const int TaskRunLevelHighest = 1;

        internal static bool IsEnabled()
        {
            try
            {
                dynamic service = CreateService();
                dynamic folder = service.GetFolder(TaskFolder);
                folder.GetTask(TaskName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        internal static bool SetEnabled(bool enabled)
        {
            try
            {
                dynamic service = CreateService();
                dynamic folder = service.GetFolder(TaskFolder);

                if (!enabled)
                {
                    folder.DeleteTask(TaskName, 0);
                    return true;
                }

                dynamic definition = service.NewTask(0);
                definition.RegistrationInfo.Description = Localization.AutostartDescription;
                definition.Settings.Enabled = true;
                definition.Settings.ExecutionTimeLimit = "PT0S";
                definition.Settings.DisallowStartIfOnBatteries = false;
                definition.Settings.StopIfGoingOnBatteries = false;

                dynamic trigger = definition.Triggers.Create(TaskTriggerLogon);
                trigger.Enabled = true;
                trigger.UserId = WindowsIdentity.GetCurrent().Name;

                dynamic action = definition.Actions.Create(TaskActionExec);
                action.Path = Assembly.GetExecutingAssembly().Location;

                definition.Principal.RunLevel = TaskRunLevelHighest;
                definition.Principal.LogonType = TaskLogonInteractiveToken;

                folder.RegisterTaskDefinition(TaskName, definition, TaskCreateOrUpdate, null, null,
                    TaskLogonInteractiveToken);
                return true;
            }
            catch (Exception e)
            {
                Trace.TraceError("Could not update the autostart task: " + e);
                return false;
            }
        }

        private static dynamic CreateService()
        {
            dynamic service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
            service.Connect();
            return service;
        }
    }
}
