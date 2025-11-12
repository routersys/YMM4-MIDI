using System;
using System.Threading.Tasks;
using ComputeSharp;
using MIDI.Utils;

namespace MIDI.Core
{
    internal static class GpuDeviceProvider
    {
        private static readonly Task<GraphicsDevice?> initializationTask;
        private static GraphicsDevice? device;

        static GpuDeviceProvider()
        {
            initializationTask = Task.Run(() =>
            {
                try
                {
                    var defaultDevice = GraphicsDevice.GetDefault();
                    Logger.Info(LogMessages.GpuDeviceInitSuccess, defaultDevice.Name);
                    return defaultDevice;
                }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.GpuDeviceInitFailed, ex);
                    return null;
                }
            });
        }

        public static async Task InitializeAsync()
        {
            device = await initializationTask;
        }

        public static GraphicsDevice? GetDevice()
        {
            if (initializationTask.IsCompleted)
            {
                return device;
            }
            return null;
        }
    }
}