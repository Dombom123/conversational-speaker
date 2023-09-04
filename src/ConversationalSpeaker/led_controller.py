import time
import threading
from rpi_ws281x import PixelStrip, Color
import argparse

# LED strip configuration:
LED_COUNT = 20
LED_PIN = 12
LED_FREQ_HZ = 800000
LED_DMA = 10
LED_BRIGHTNESS = 100
LED_INVERT = False
LED_CHANNEL = 0

# Global event for stopping the thread
stop_event = threading.Event()

def colorWipe(strip, color, wait_ms=50):
    """Wipe color across display a pixel at a time."""
    for i in range(strip.numPixels()):
        strip.setPixelColor(i, color)
        strip.show()
        time.sleep(wait_ms / 1000.0)


def theaterChase(strip, color, wait_ms=50):
    """Movie theater light style chaser animation."""
    for q in range(3):
        for i in range(0, strip.numPixels(), 3):
            strip.setPixelColor(i + q, color)
        strip.show()
        time.sleep(wait_ms / 1000.0)
        for i in range(0, strip.numPixels(), 3):
            strip.setPixelColor(i + q, 0)


def rainbowCycle(strip, wait_ms=20, iterations=1):
    """Draw rainbow that uniformly distributes itself across all pixels."""
    for j in range(256 * iterations):
        for i in range(strip.numPixels()):
            strip.setPixelColor(i, wheel(
                (int(i * 256 / strip.numPixels()) + j) & 255))
        strip.show()
        time.sleep(wait_ms / 1000.0)


def wheel(pos):
    """Generate rainbow colors across 0-255 positions."""
    if pos < 85:
        return Color(pos * 3, 255 - pos * 3, 0)
    elif pos < 170:
        pos -= 85
        return Color(255 - pos * 3, 0, pos * 3)
    else:
        pos -= 170
        return Color(0, pos * 3, 255 - pos * 3)

def colorPulse(strip, color, pulse_iterations=3, wait_ms=50):
    """Pulse a color in and out."""
    for j in range(pulse_iterations):
        # Fade in
        for k in range(0, 256, 10):
            for i in range(strip.numPixels()):
                strip.setPixelColor(i, Color(int(color[0] * k / 255), int(color[1] * k / 255), int(color[2] * k / 255)))
            strip.show()
            time.sleep(wait_ms / 1000.0)

        # Fade out
        for k in range(255, -1, -10):
            for i in range(strip.numPixels()):
                strip.setPixelColor(i, Color(int(color[0] * k / 255), int(color[1] * k / 255), int(color[2] * k / 255)))
            strip.show()
            time.sleep(wait_ms / 1000.0)


def set_color(strip, color):
    """Set the entire strip to a single color."""
    for i in range(strip.numPixels()):
        strip.setPixelColor(i, color)
    strip.show()


def idle_state(strip):
    """Idle state animation: Rainbow cycle to show standby."""
    while not stop_event.is_set():
        rainbowCycle(strip)

def listening_state(strip):
    """Listening state: Theater chase in green to show active listening."""
    while not stop_event.is_set():
        theaterChase(strip, Color(0, 255, 0))

def thinking_state(strip):
    """Thinking state animation: Color pulse in blue to show processing."""
    while not stop_event.is_set():
        colorPulse(strip, [0, 0, 255])

def responding_state(strip):
    """Responding state animation: Theater chase in red to show response."""
    while not stop_event.is_set():
        theaterChase(strip, Color(255, 0, 0))

def clear_state(strip):
    """Clear state: Turn off all the LEDs."""
    set_color(strip, Color(0, 0, 0))
    stop_event.set()

def led_controller(action):
    global stop_event
    stop_event.clear()  # Reset the stop event

    strip = PixelStrip(LED_COUNT, LED_PIN, LED_FREQ_HZ, LED_DMA, LED_INVERT, LED_BRIGHTNESS, LED_CHANNEL)
    strip.begin()

    if action == 'idle':
        idle_state(strip)
    elif action == 'listening':
        listening_state(strip)
    elif action == 'thinking':
        thinking_state(strip)
    elif action == 'responding':
        responding_state(strip)
    elif action == 'clear':
        clear_state(strip)

    # If you want the clear animation to also turn off the LEDs at the end, uncomment the following:
    # colorWipe(strip, Color(0, 0, 0), 10)

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('-a', '--action', type=str, help='LED action to perform', required=True)
    args = parser.parse_args()

    thread = threading.Thread(target=led_controller, args=(args.action,))
    thread.start()
    thread.join()  # This will ensure the main thread waits for the animation thread to complete