using System;

namespace ConversationalSpeaker.PlatformAbstractions
{
    public class KeyboardStartTrigger : IStartTrigger
    {
        public event EventHandler Triggered;

        public KeyboardStartTrigger()
        {
            // Start a separate thread or task to listen to keyboard input
            // When a specific key is pressed, invoke the Triggered event
        }
    }
}
