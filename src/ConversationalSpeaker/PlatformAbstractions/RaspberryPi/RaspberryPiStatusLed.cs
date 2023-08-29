using System;
using rpi_ws281x;
namespace ConversationalSpeaker.PlatformAbstractions
{

    public class RaspberryPiStatusLed : IStatusLed
    {
        private const int LED_COUNT = 1;  // Number of LEDs in strip or ring.
        private const int LED_PIN = 18;   // GPIO pin connected to the pixels (Must support PWM!).
        private const int LED_FREQ_HZ = 800000;  // LED signal frequency in hertz.
        private const int LED_DMA_NUM = 5;  // DMA channel to use for generating signal.
        private const int BRIGHTNESS = 255;  // Set to 0 for darkest and 255 for brightest.
        private const int LED_INVERT = 0;    // Set to 1 to invert signal.

        private readonly WS281x rpiLed;

        public RaspberryPiStatusLed()
        {
            var settings = new Settings
            {
                Channel = new Channel
                {
                    GPIO_PIN = LED_PIN,
                    LED_COUNT = LED_COUNT,
                    BRIGHTNESS = BRIGHTNESS,
                    STRIP_TYPE = StripType.WS2811_STRIP_GRB
                }
            };

            rpiLed = new WS281x(settings);
        }

        public void Initialize()
        {
            SetColor(Color.Blue); // Assuming blue is the idle color.
        }

        public void Listening()
        {
            SetColor(Color.Green);
        }

        public void Processing()
        {
            SetColor(Color.Yellow);
        }

        public void ResponseReady()
        {
            SetColor(Color.Cyan);
        }

        public void Error()
        {
            // You can make it flash red, for example
            for (int i = 0; i < 3; i++)
            {
                SetColor(Color.Red);
                System.Threading.Thread.Sleep(300);
                TurnOff();
                System.Threading.Thread.Sleep(300);
            }
        }

        public void Idle()
        {
            SetColor(Color.Blue);
        }

        public void TurnOn()
        {
            SetColor(Color.White);  // or any other default color for "On"
        }

        public void TurnOff()
        {
            rpiLed.Reset();
        }

        private void SetColor(Color color)
        {
            for (int i = 0; i < LED_COUNT; i++)
            {
                rpiLed.SetLED(i, color);
            }
            rpiLed.Render();
        }
    }
}