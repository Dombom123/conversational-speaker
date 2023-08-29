using System;

namespace ConversationalSpeaker.PlatformAbstractions
{
    public class MockStatusLed : IStatusLed
    {
        public void Initialize()
        {
            Console.WriteLine("Mock LED initialized (Idle color)");
        }

        public void Listening()
        {
            Console.WriteLine("Mock LED in Listening state (e.g., green color)");
        }

        public void Processing()
        {
            Console.WriteLine("Mock LED in Processing state (e.g., pulsating yellow)");
        }

        public void ResponseReady()
        {
            Console.WriteLine("Mock LED indicating Response Ready (e.g., cyan color)");
        }

        public void Error()
        {
            Console.WriteLine("Mock LED indicating Error (e.g., flashing red)");
        }

        public void Idle()
        {
            Console.WriteLine("Mock LED returned to Idle state (e.g., blue color)");
        }

        // If you still need these original methods, they're here:
        public void TurnOn()
        {
            Console.WriteLine("Mock LED turned on");
        }

        public void TurnOff()
        {
            Console.WriteLine("Mock LED turned off");
        }
    }
}