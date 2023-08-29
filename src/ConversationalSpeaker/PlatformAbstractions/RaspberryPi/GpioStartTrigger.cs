using System;
namespace ConversationalSpeaker.PlatformAbstractions
{
    public class GpioStartTrigger : IStartTrigger
    {
        public event EventHandler Triggered;

        public GpioStartTrigger()
        {
            // Initialize GPIO listening here
            // When GPIO button is pressed, invoke the Triggered event
        }
    }
}
