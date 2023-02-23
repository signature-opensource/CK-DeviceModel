namespace CK.DeviceModel.Tests
{
    public class ScaleTestSendLogsFromCommandAndEventLoopCommand : DeviceCommand<ScaleHost>
    {
        protected override DeviceCommandStoppedBehavior StoppedBehavior => DeviceCommandStoppedBehavior.RunAnyway;
    }
}
